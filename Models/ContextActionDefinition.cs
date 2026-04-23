using System;
using System.Collections.Generic;
using System.Linq;

namespace GuaranteeManager.Models
{
    public enum ContextActionResultKind
    {
        InsightWindow,
        DecisionDialog,
        ManagedReferenceWindow,
        Navigation,
        ExternalDocument,
        Export,
        Clipboard,
        Destructive
    }

    public enum ContextActionFlowProfile
    {
        Immediate,
        Guided,
        Workflow
    }

    public readonly record struct ContextActionAvailability(bool IsEnabled, string? DisabledReason)
    {
        public static ContextActionAvailability Enabled() => new(true, null);

        public static ContextActionAvailability Disabled(string reason) => new(false, reason);
    }

    public sealed class ContextActionDefinition
    {
        public string? Id { get; }
        public string Header { get; }
        public string Description { get; }
        public ContextActionResultKind? ResultKind { get; }
        public bool IsDestructive { get; }
        public IReadOnlyList<ContextActionDefinition> Children { get; }
        public bool IsLeaf => !string.IsNullOrWhiteSpace(Id);
        public bool HasChildren => Children.Count > 0;
        public ContextActionFlowProfile? FlowProfile => ResultKind switch
        {
            null => null,
            ContextActionResultKind.DecisionDialog => ContextActionFlowProfile.Workflow,
            ContextActionResultKind.Export => ContextActionFlowProfile.Guided,
            ContextActionResultKind.Destructive => ContextActionFlowProfile.Guided,
            _ => ContextActionFlowProfile.Immediate
        };
        public string StepHint => ResultKind switch
        {
            null => string.Empty,
            ContextActionResultKind.DecisionDialog => "عدة مراحل",
            ContextActionResultKind.Export => "خطوتان غالبًا",
            ContextActionResultKind.Destructive => "خطوتان غالبًا",
            _ => "خطوة واحدة"
        };
        public string DeliveryHint => ResultKind switch
        {
            null => string.Empty,
            ContextActionResultKind.InsightWindow => "يعرض جوابًا داخل البرنامج.",
            ContextActionResultKind.ManagedReferenceWindow => "يفتح نافذة مرجعية داخل البرنامج.",
            ContextActionResultKind.Navigation => "ينقلك إلى شاشة أو سجل داخل البرنامج.",
            ContextActionResultKind.ExternalDocument => "يفتح ملفًا خارجيًا.",
            ContextActionResultKind.Export => "يفتح حفظ الملف ثم ينشئ ملفًا خارجيًا.",
            ContextActionResultKind.DecisionDialog => "يبدأ إجراءً عبر نافذة إدخال أو قرار.",
            ContextActionResultKind.Clipboard => "ينسخ القيمة مباشرة إلى الحافظة.",
            ContextActionResultKind.Destructive => "يتطلب تأكيدًا قبل تنفيذ الأثر على البيانات.",
            _ => string.Empty
        };
        public string PolicyTooltip
        {
            get
            {
                if (!IsLeaf)
                {
                    return Description;
                }

                string deliveryHint = DeliveryHint;
                string stepHint = StepHint;

                if (string.IsNullOrWhiteSpace(deliveryHint) && string.IsNullOrWhiteSpace(stepHint))
                {
                    return Description;
                }

                if (string.IsNullOrWhiteSpace(Description))
                {
                    return $"{stepHint} - {deliveryHint}".Trim(' ', '-');
                }

                return $"{Description}{Environment.NewLine}{stepHint} - {deliveryHint}";
            }
        }

        private ContextActionDefinition(
            string? id,
            string header,
            string description,
            ContextActionResultKind? resultKind,
            bool isDestructive,
            IReadOnlyList<ContextActionDefinition> children)
        {
            Id = id;
            Header = header;
            Description = description;
            ResultKind = resultKind;
            IsDestructive = isDestructive;
            Children = children;
        }

        public static ContextActionDefinition Group(string header, string description, params ContextActionDefinition[] children)
        {
            return new ContextActionDefinition(
                id: null,
                header,
                description,
                resultKind: null,
                isDestructive: false,
                children.Where(child => child != null).ToArray());
        }

        public static ContextActionDefinition Action(
            string id,
            string header,
            ContextActionResultKind resultKind,
            string description,
            bool isDestructive = false)
        {
            return new ContextActionDefinition(
                id,
                header,
                description,
                resultKind,
                isDestructive,
                Array.Empty<ContextActionDefinition>());
        }
    }

    public sealed class ContextActionSection
    {
        public string Header { get; }
        public string Description { get; }
        public IReadOnlyList<ContextActionDefinition> Items { get; }

        public ContextActionSection(string header, string description, params ContextActionDefinition[] items)
        {
            Header = header;
            Description = description;
            Items = items.Where(item => item != null).ToArray();
        }
    }
}
