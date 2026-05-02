using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.Utils;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed class OperationalInquiryDialog : Window
    {
        private readonly OperationalInquiryResult _result;
        private readonly IDatabaseService _database;
        private readonly IWorkflowService _workflow;
        private readonly IExcelService _excel;
        private readonly Border _nextStepCard = new();
        private readonly TextBlock _nextStepSummary = WorkspaceSurfaceChrome.Text(12, FontWeights.Medium, "#0F172A");
        private readonly TextBlock _nextStepHint = WorkspaceSurfaceChrome.Text(11, FontWeights.Normal, "#64748B");
        private readonly Button _nextStepActionButton = new();
        private Action? _nextStepAction;

        private OperationalInquiryDialog(
            OperationalInquiryResult result,
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel)
        {
            _result = result;
            _database = database;
            _workflow = workflow;
            _excel = excel;

            Title = "الاستعلامات التشغيلية";
            UiInstrumentation.Identify(this, "Dialog.OperationalInquiry", Title);
            UiInstrumentation.Identify(_nextStepSummary, "Dialog.OperationalInquiry.NextStepSummary", "ملخص الخطوة التالية");
            UiInstrumentation.Identify(_nextStepHint, "Dialog.OperationalInquiry.NextStepHint", "شرح الخطوة التالية");
            UiInstrumentation.Identify(_nextStepActionButton, "Dialog.OperationalInquiry.NextStepButton", "زر الخطوة التالية");
            Width = 980;
            Height = 680;
            MinWidth = 860;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            FlowDirection = FlowDirection.RightToLeft;
            FontFamily = UiTypography.DefaultFontFamily;
            Background = WorkspaceSurfaceChrome.BrushFrom("#F7F9FC");
            DialogWindowSupport.Attach(this, nameof(OperationalInquiryDialog));

            Content = BuildLayout();
            ConfigureNextStepState();
        }

        public static void ShowFor(
            OperationalInquiryResult result,
            IDatabaseService database,
            IWorkflowService workflow,
            IExcelService excel)
        {
            int inquiryRootId = result.CurrentGuarantee?.RootId
                ?? result.SelectedGuarantee?.RootId
                ?? result.ResultGuarantee?.RootId
                ?? 0;
            string windowKey = inquiryRootId > 0
                ? $"inquiry:{inquiryRootId}:{result.InquiryKey}"
                : $"inquiry:{result.InquiryKey}:{result.Subject}";

            App.CurrentApp.GetRequiredService<SecondaryWindowManager>().ShowDialog(
                windowKey,
                () => new OperationalInquiryDialog(result, database, workflow, excel),
                "الاستعلامات التشغيلية",
                "نتيجة هذا الاستعلام مفتوحة بالفعل.");
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(BuildHeader());

            UIElement summary = BuildSummaryStrip();
            Grid.SetRow(summary, 2);
            root.Children.Add(summary);

            UIElement content = BuildMainContent();
            Grid.SetRow(content, 4);
            root.Children.Add(content);

            UIElement actions = BuildActions();
            Grid.SetRow(actions, 6);
            root.Children.Add(actions);

            return root;
        }

        private UIElement BuildHeader()
        {
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = _result.Title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = _result.Subject,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                Margin = new Thickness(0, 4, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = $"آخر حدث مرجعي: {_result.EventDateLabel}",
                FontSize = 11,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3B8"),
                Margin = new Thickness(0, 4, 0, 0)
            });
            header.Children.Add(titleStack);

            var closeButton = WorkspaceSurfaceChrome.ActionButton("إغلاق", "White", "#D8E1EE", "#0F172A");
            UiInstrumentation.Identify(closeButton, "Dialog.OperationalInquiry.CloseButton", "إغلاق الاستعلام التشغيلي");
            closeButton.MinWidth = 92;
            closeButton.Click += (_, _) => Close();
            Grid.SetColumn(closeButton, 1);
            header.Children.Add(closeButton);
            return header;
        }

        private UIElement BuildSummaryStrip()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            int evidenceCount = _result.Facts.Count(fact => !string.IsNullOrWhiteSpace(fact.Value) && fact.Value != "---");

            var metrics = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 4
            };
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("تاريخ الحدث", _result.EventDateLabel.Split(' ')[0], "#2563EB"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("خطاب الطلب", _result.CanOpenRequestLetter ? "موجود" : "غير متاح", "#E09408"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("رد البنك", _result.CanOpenResponseDocument ? "موجود" : "غير متاح", "#16A34A"));
            metrics.Children.Add(WorkspaceSurfaceChrome.MetricCard("الأدلة", evidenceCount.ToString("N0", CultureInfo.InvariantCulture), guarantee != null ? "#0F172A" : "#64748B"));
            WorkspaceSurfaceChrome.ApplyMetricCardSpacing(metrics);
            return metrics;
        }

        private UIElement BuildMainContent()
        {
            var grid = new Grid { FlowDirection = FlowDirection.LeftToRight };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            UiInstrumentation.Identify(scroll, "Dialog.OperationalInquiry.ContentScrollViewer", "تمرير الاستعلام التشغيلي");

            var leftStack = new StackPanel();
            leftStack.Children.Add(BuildAnswerCard());
            leftStack.Children.Add(BuildFactsCard());
            leftStack.Children.Add(BuildTimelineCard());
            scroll.Content = leftStack;
            grid.Children.Add(scroll);

            UIElement detailPanel = UiInstrumentation.Identify(
                BuildDetailPanel(),
                "Dialog.OperationalInquiry.DetailPanel",
                "لوحة تفاصيل الاستعلام التشغيلي");
            Grid.SetColumn(detailPanel, 1);
            grid.Children.Add(detailPanel);
            return grid;
        }

        private UIElement BuildAnswerCard()
        {
            Border card = WorkspaceSurfaceChrome.Card(new Thickness(14));
            UiInstrumentation.Identify(card, "Dialog.OperationalInquiry.AnswerCard", "بطاقة الجواب المختصر");
            card.Margin = new Thickness(0, 0, 0, 10);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "الجواب المختصر",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            stack.Children.Add(new TextBlock
            {
                Text = _result.Answer,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock
            {
                Text = _result.Explanation,
                FontSize = 12,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                Margin = new Thickness(0, 10, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            card.Child = stack;
            return card;
        }

        private UIElement BuildFactsCard()
        {
            Border card = WorkspaceSurfaceChrome.Card(new Thickness(14));
            UiInstrumentation.Identify(card, "Dialog.OperationalInquiry.FactsCard", "بطاقة الأدلة");
            card.Margin = new Thickness(0, 0, 0, 10);

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "ملخص الأدلة",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            stack.Children.Add(WorkspaceSurfaceChrome.Divider());

            if (_result.Facts.Count == 0)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "لا توجد حقائق إضافية مرتبطة بهذا الاستعلام.",
                    FontSize = 11,
                    Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B")
                });
            }
            else
            {
                var wrap = new WrapPanel
                {
                    Orientation = Orientation.Horizontal
                };

                foreach (OperationalInquiryFact fact in _result.Facts)
                {
                    wrap.Children.Add(BuildFactTile(fact));
                }

                stack.Children.Add(wrap);
            }

            card.Child = stack;
            return card;
        }

        private UIElement BuildTimelineCard()
        {
            Border card = WorkspaceSurfaceChrome.Card(new Thickness(14));
            UiInstrumentation.Identify(card, "Dialog.OperationalInquiry.TimelineCard", "بطاقة التسلسل الزمني");

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "التسلسل الزمني الداعم",
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
            });
            stack.Children.Add(WorkspaceSurfaceChrome.Divider());

            if (!_result.HasTimeline)
            {
                stack.Children.Add(new TextBlock
                {
                    Text = "لا يوجد خط زمني إضافي لهذا الاستعلام.",
                    FontSize = 11,
                    Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B")
                });
            }
            else
            {
                foreach (OperationalInquiryTimelineEntry entry in _result.Timeline.OrderByDescending(item => item.Timestamp))
                {
                    stack.Children.Add(BuildTimelineRow(entry));
                }
            }

            card.Child = stack;
            return card;
        }

        private UIElement BuildDetailPanel()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            Tone tone = ResolveTone(guarantee);
            string statusText = BuildStatusText(guarantee);
            string bank = guarantee?.Bank ?? _result.CurrentGuarantee?.Bank ?? _result.SelectedGuarantee?.Bank ?? string.Empty;
            string beneficiary = guarantee == null
                ? _result.Subject
                : BusinessPartyDefaults.NormalizeBeneficiary(guarantee.Beneficiary);
            string headline = guarantee == null
                ? _result.EventDateLabel.Split(' ')[0]
                : ArabicAmountFormatter.FormatSaudiRiyals(guarantee.Amount);
            string caption = guarantee == null
                ? "تاريخ الحدث المرجعي"
                : "القيمة الحالية للسجل المرتبط";
            string relatedRequest = _result.RelatedRequest == null
                ? "---"
                : $"{_result.RelatedRequest.TypeLabel} #{_result.RelatedRequest.SequenceNumber.ToString("N0", CultureInfo.InvariantCulture)}";
            string resultContextLabel = BuildResultContextLabel(_result);
            string resultContextValue = BuildResultContextValue(_result);
            TextBlock detailGuaranteeNo = WorkspaceSurfaceChrome.Text(18, FontWeights.Bold, "#0F172A");
            detailGuaranteeNo.Text = guarantee?.GuaranteeNo ?? _result.Title;
            detailGuaranteeNo.Margin = new Thickness(0, 8, 0, 0);

            TextBlock detailSubject = WorkspaceSurfaceChrome.Text(11, FontWeights.Medium, "#64748B");
            detailSubject.Text = _result.Subject;
            detailSubject.Margin = new Thickness(0, 4, 0, 0);

            TextBlock detailHeadline = WorkspaceSurfaceChrome.Text(32, FontWeights.Bold, "#0F172A");
            detailHeadline.Text = headline;
            detailHeadline.Margin = new Thickness(0, 10, 0, 0);

            TextBlock detailCaption = WorkspaceSurfaceChrome.Text(11, FontWeights.Normal, "#94A3B8");
            detailCaption.Text = caption;
            detailCaption.Margin = new Thickness(0, 3, 0, 0);

            var statusTextBlock = new TextBlock
            {
                Text = statusText,
                FontSize = 10,
                FontWeight = FontWeights.Medium,
                Foreground = TonePalette.Foreground(tone)
            };

            var statusBorder = new Border
            {
                Style = WorkspaceSurfaceChrome.Style("StatusPill"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 12),
                Background = TonePalette.Background(tone),
                BorderBrush = TonePalette.Border(tone),
                Child = statusTextBlock
            };

            var bankRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                FlowDirection = FlowDirection.RightToLeft,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            bankRow.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(beneficiary) ? bank : $"{bank} | {beneficiary}",
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                VerticalAlignment = VerticalAlignment.Center
            });
            bankRow.Children.Add(new Image
            {
                Source = GuaranteeRow.ResolveBankLogo(bank),
                Width = 17,
                Height = 17,
                Margin = new Thickness(7, 0, 0, 0)
            });

            var content = new StackPanel
            {
                Margin = new Thickness(16, 14, 16, 14),
                Children =
                {
                    new TextBlock
                    {
                        Text = "سياق النتيجة",
                        FontSize = 16,
                        FontWeight = FontWeights.Medium,
                        Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A")
                    },
                    detailGuaranteeNo,
                    detailSubject,
                    statusBorder,
                    bankRow,
                    detailHeadline,
                    detailCaption,
                    BuildNextStepCard(),
                    WorkspaceSurfaceChrome.Divider(),
                    BuildInfoLine("الاستعلام", _result.Title),
                    BuildInfoLine("الموضوع", _result.Subject),
                    BuildInfoLine("رقم الضمان", guarantee?.GuaranteeNo ?? "---"),
                    BuildInfoLine("الطلب المرتبط", relatedRequest),
                    BuildInfoLine(resultContextLabel, resultContextValue),
                    BuildInfoLine("آخر حدث", _result.EventDateLabel),
                    BuildInfoBlock("تفسير إضافي", _result.Explanation)
                }
            };

            return WorkspaceSurfaceChrome.BuildReferenceDetailPanel(content);
        }

        private static string BuildResultContextLabel(OperationalInquiryResult result)
        {
            WorkflowRequest? request = result.RelatedRequest;
            if (request == null || request.Status != RequestStatus.Executed)
            {
                return "الإصدار الحالي";
            }

            return request.Type switch
            {
                RequestType.Extension or RequestType.Reduction => "الإصدار الناتج",
                RequestType.Replacement => "الضمان البديل",
                RequestType.Release or RequestType.Liquidation => "أثر التنفيذ",
                RequestType.Verification when request.ResultVersionId.HasValue => "المستند المعتمد",
                _ => "أثر الطلب"
            };
        }

        private static string BuildResultContextValue(OperationalInquiryResult result)
        {
            WorkflowRequest? request = result.RelatedRequest;
            if (request == null || request.Status != RequestStatus.Executed)
            {
                return result.CurrentGuarantee?.VersionLabel ?? "---";
            }

            return request.Type switch
            {
                RequestType.Replacement =>
                    result.ResultGuarantee?.GuaranteeNo
                    ?? (string.IsNullOrWhiteSpace(request.ReplacementGuaranteeNo) ? "---" : request.ReplacementGuaranteeNo),
                RequestType.Release =>
                    result.CurrentGuarantee?.LifecycleStatusLabel ?? "مفرج",
                RequestType.Liquidation =>
                    result.CurrentGuarantee?.LifecycleStatusLabel ?? "مسيّل",
                RequestType.Verification when request.ResultVersionId.HasValue =>
                    result.ResultGuarantee?.VersionLabel ?? "مرفق رسمي",
                _ =>
                    result.ResultGuarantee?.VersionLabel
                    ?? result.CurrentGuarantee?.VersionLabel
                    ?? "---"
            };
        }

        private UIElement BuildNextStepCard()
        {
            _nextStepCard.Background = WorkspaceSurfaceChrome.BrushFrom("#EFF6FF");
            _nextStepCard.BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#BFDBFE");
            _nextStepCard.BorderThickness = new Thickness(1);
            _nextStepCard.CornerRadius = new CornerRadius(8);
            _nextStepCard.Padding = new Thickness(12, 10, 12, 10);
            _nextStepCard.Margin = new Thickness(0, 10, 0, 0);

            _nextStepActionButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            _nextStepActionButton.MinWidth = 132;
            _nextStepActionButton.Height = 32;
            _nextStepActionButton.HorizontalAlignment = HorizontalAlignment.Right;
            _nextStepActionButton.Margin = new Thickness(0, 10, 0, 0);
            _nextStepActionButton.Click += (_, _) => _nextStepAction?.Invoke();

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "الخطوة التالية",
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#2563EB"),
                Margin = new Thickness(0, 0, 0, 6)
            });
            stack.Children.Add(_nextStepSummary);
            _nextStepHint.Margin = new Thickness(0, 4, 0, 0);
            stack.Children.Add(_nextStepHint);
            stack.Children.Add(_nextStepActionButton);
            _nextStepCard.Child = stack;
            return _nextStepCard;
        }

        private UIElement BuildActions()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            bool hasAttachments = guarantee?.Attachments?.Count > 0;
            bool canOpenGuaranteeContext = CanOpenGuaranteeContext();
            string contextButtonText = "الضمانات";
            string contextButtonHint = "يفتح الضمان في المحفظة عند السجل الزمني المناسب لسياق هذا الجواب.";
            if (TryResolveGuaranteeContextHandoff(
                    out _,
                    out _,
                    out GuaranteeFocusArea contextArea,
                    out int? contextRequestId,
                    out _))
            {
                contextButtonText = BuildGuaranteeContextButtonText(contextArea, contextRequestId);
                contextButtonHint = $"يفتح {BuildGuaranteeHandoffSectionText(contextArea, contextRequestId)}.";
            }

            Button openGuaranteeContextButton = WorkspaceSurfaceChrome.ActionButton(contextButtonText, "White", "#D8E1EE", "#0F172A");
            openGuaranteeContextButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(openGuaranteeContextButton, "Dialog.OperationalInquiry.OpenGuaranteeContextButton", "فتح وجهة الضمان من الاستعلام");
            openGuaranteeContextButton.IsEnabled = canOpenGuaranteeContext;
            openGuaranteeContextButton.ToolTip = canOpenGuaranteeContext
                ? contextButtonHint
                : "لا يوجد ضمان محدد يمكن فتحه من هذا الجواب.";
            ToolTipService.SetShowOnDisabled(openGuaranteeContextButton, true);
            openGuaranteeContextButton.Click += (_, _) => OpenGuaranteeContext();

            Button attachmentsButton = WorkspaceSurfaceChrome.ActionButton("مرفقات الإصدار", "White", "#D8E1EE", "#0F172A");
            attachmentsButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(attachmentsButton, "Dialog.OperationalInquiry.OpenAttachmentsButton", "فتح مرفقات الإصدار");
            attachmentsButton.IsEnabled = hasAttachments;
            attachmentsButton.ToolTip = hasAttachments
                ? "يفتح مرفقات الإصدار الحالي للضمان."
                : "لا توجد مرفقات مرتبطة بالإصدار الحالي لهذا الضمان.";
            ToolTipService.SetShowOnDisabled(attachmentsButton, true);
            attachmentsButton.Click += (_, _) => OpenAttachments();

            Button openLetterButton = WorkspaceSurfaceChrome.ActionButton("خطاب الطلب", "White", "#D8E1EE", "#0F172A");
            openLetterButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(openLetterButton, "Dialog.OperationalInquiry.OpenLetterButton", "فتح خطاب الطلب");
            openLetterButton.IsEnabled = _result.CanOpenRequestLetter;
            openLetterButton.ToolTip = _result.CanOpenRequestLetter
                ? "يفتح خطاب الطلب المرتبط بهذا الجواب."
                : "لا يوجد خطاب طلب مرتبط بالسجل أو الطلب الذي استند إليه هذا الجواب.";
            ToolTipService.SetShowOnDisabled(openLetterButton, true);
            openLetterButton.Click += (_, _) => OpenLetter();

            Button openResponseButton = WorkspaceSurfaceChrome.ActionButton("رد البنك", "White", "#D8E1EE", "#0F172A");
            openResponseButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(openResponseButton, "Dialog.OperationalInquiry.OpenResponseButton", "فتح رد البنك");
            openResponseButton.IsEnabled = _result.CanOpenResponseDocument;
            openResponseButton.ToolTip = _result.CanOpenResponseDocument
                ? "يفتح مستند رد البنك المرتبط بهذا الجواب."
                : "لا يوجد مستند رد بنك مرتبط بالسجل أو الطلب الذي استند إليه هذا الجواب.";
            ToolTipService.SetShowOnDisabled(openResponseButton, true);
            openResponseButton.Click += (_, _) => OpenResponse();

            Button exportButton = WorkspaceSurfaceChrome.ActionButton("تقرير الضمان", "White", "#D8E1EE", "#0F172A");
            exportButton.Style = WorkspaceSurfaceChrome.Style("BaseButton");
            UiInstrumentation.Identify(exportButton, "Dialog.OperationalInquiry.ExportReportButton", "تصدير تقرير الضمان");
            exportButton.IsEnabled = guarantee != null;
            exportButton.ToolTip = guarantee != null
                ? "يصدر تقرير الضمان الحالي."
                : "لا يوجد ضمان محدد حاليًا لتصدير تقريره.";
            ToolTipService.SetShowOnDisabled(exportButton, true);
            exportButton.Click += (_, _) => ExportGuaranteeReport();

            Button closeButton = WorkspaceSurfaceChrome.ActionButton("إغلاق", "#2563EB", "#2563EB", "White");
            closeButton.Style = WorkspaceSurfaceChrome.Style("PrimaryButton");
            UiInstrumentation.Identify(closeButton, "Dialog.OperationalInquiry.CloseFooterButton", "إغلاق الاستعلام التشغيلي");
            closeButton.Click += (_, _) => Close();

            var actions = new System.Windows.Controls.Primitives.UniformGrid
            {
                Columns = 6,
                FlowDirection = FlowDirection.LeftToRight
            };

            foreach (Button button in new[]
                     {
                         openGuaranteeContextButton,
                         attachmentsButton,
                         openLetterButton,
                         openResponseButton,
                         exportButton,
                         closeButton
                     })
            {
                button.Margin = new Thickness(0, 0, 8, 0);
                actions.Children.Add(button);
            }

            return actions;
        }

        private Border BuildFactTile(OperationalInquiryFact fact)
        {
            var border = new Border
            {
                Background = WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 8, 8),
                Width = 210
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = fact.Label,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3B8"),
                TextAlignment = TextAlignment.Right
            });
            stack.Children.Add(new TextBlock
            {
                Text = fact.Value,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap,
                TextAlignment = TextAlignment.Right
            });
            border.Child = stack;
            return border;
        }

        private Border BuildTimelineRow(OperationalInquiryTimelineEntry entry)
        {
            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid { FlowDirection = FlowDirection.LeftToRight };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var date = new TextBlock
            {
                Text = entry.TimestampLabel,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                VerticalAlignment = VerticalAlignment.Top,
                FlowDirection = FlowDirection.LeftToRight
            };
            grid.Children.Add(date);

            var stack = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };
            stack.Children.Add(new TextBlock
            {
                Text = entry.Title,
                FontSize = 12,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#0F172A"),
                TextWrapping = TextWrapping.Wrap
            });
            stack.Children.Add(new TextBlock
            {
                Text = entry.Details,
                FontSize = 11,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#64748B"),
                Margin = new Thickness(0, 5, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(stack, 1);
            grid.Children.Add(stack);

            border.Child = grid;
            return border;
        }

        private static Grid BuildInfoLine(string label, string value)
        {
            TextBlock valueBlock = WorkspaceSurfaceChrome.Text(12, FontWeights.Medium, "#0F172A");
            valueBlock.Text = value;
            return WorkspaceSurfaceChrome.InfoLine(label, valueBlock);
        }

        private static Border BuildInfoBlock(string title, string value)
        {
            var body = WorkspaceSurfaceChrome.Text(11, FontWeights.Normal, "#64748B");
            body.Text = value;

            var border = new Border
            {
                Background = WorkspaceSurfaceChrome.BrushFrom("#F8FAFC"),
                BorderBrush = WorkspaceSurfaceChrome.BrushFrom("#E3E9F2"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 8, 0, 0)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 11,
                FontWeight = FontWeights.Medium,
                Foreground = WorkspaceSurfaceChrome.BrushFrom("#94A3B8"),
                Margin = new Thickness(0, 0, 0, 6)
            });
            stack.Children.Add(body);
            border.Child = stack;
            return border;
        }

        private Guarantee? ResolveFocusGuarantee()
        {
            int? guaranteeId = _result.ResultGuarantee?.Id
                ?? _result.CurrentGuarantee?.Id
                ?? _result.SelectedGuarantee?.Id;

            if (!guaranteeId.HasValue)
            {
                return null;
            }

            return _database.GetGuaranteeById(guaranteeId.Value)
                ?? _result.ResultGuarantee
                ?? _result.CurrentGuarantee
                ?? _result.SelectedGuarantee;
        }

        private Tone ResolveTone(Guarantee? guarantee)
        {
            if (_result.RelatedRequest != null)
            {
                return _result.RelatedRequest.Status switch
                {
                    RequestStatus.Executed => Tone.Success,
                    RequestStatus.Pending => Tone.Warning,
                    RequestStatus.Rejected => Tone.Danger,
                    RequestStatus.Cancelled => Tone.Info,
                    RequestStatus.Superseded => Tone.Info,
                    _ => Tone.Info
                };
            }

            if (guarantee == null)
            {
                return Tone.Info;
            }

            if (guarantee.IsExpired && guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active)
            {
                return Tone.Danger;
            }

            if (guarantee.IsExpiringSoon)
            {
                return Tone.Warning;
            }

            return guarantee.LifecycleStatus == GuaranteeLifecycleStatus.Active
                ? Tone.Success
                : Tone.Info;
        }

        private string BuildStatusText(Guarantee? guarantee)
        {
            if (_result.RelatedRequest != null)
            {
                return _result.RelatedRequest.StatusLabel;
            }

            if (guarantee != null)
            {
                return guarantee.LifecycleStatusLabel;
            }

            return "جواب تشغيلي";
        }

        private bool CanOpenGuaranteeContext()
        {
            return ResolveFocusGuarantee() != null
                   && Application.Current.MainWindow?.DataContext is ShellViewModel;
        }

        private bool TryResolveGuaranteeContextHandoff(
            out ShellViewModel shell,
            out GuaranteeRow row,
            out GuaranteeFocusArea focusArea,
            out int? requestIdToFocus,
            out bool hasInquiryRoute)
        {
            shell = null!;
            row = null!;
            focusArea = GuaranteeFocusArea.None;
            requestIdToFocus = null;
            hasInquiryRoute = false;

            Guarantee? guarantee = ResolveFocusGuarantee();
            if (guarantee == null ||
                Application.Current.MainWindow?.DataContext is not ShellViewModel shellViewModel)
            {
                return false;
            }

            int rootId = guarantee.RootId ?? guarantee.Id;
            Guarantee currentGuarantee = _database.GetCurrentGuaranteeByRootId(rootId) ?? guarantee;
            List<WorkflowRequest> requests = _database.GetWorkflowRequestsByRootId(rootId);
            row = GuaranteeRow.FromGuarantee(currentGuarantee, requests);

            hasInquiryRoute = InquiryContextRoutingResolver.TryResolve(_result, out focusArea, out requestIdToFocus);
            if (!hasInquiryRoute)
            {
                focusArea = row.SuggestedFocusArea == GuaranteeFocusArea.None
                    ? GuaranteeFocusArea.ExecutiveSummary
                    : row.SuggestedFocusArea;
                requestIdToFocus = null;
            }

            shell = shellViewModel;
            return true;
        }

        private static string BuildGuaranteeContextButtonText(GuaranteeFocusArea focusArea, int? requestIdToFocus)
        {
            return focusArea switch
            {
                GuaranteeFocusArea.Requests => requestIdToFocus.HasValue ? "فتح حدث الطلب" : "السجل الزمني",
                GuaranteeFocusArea.Outputs => "السجل الزمني",
                GuaranteeFocusArea.Attachments => "المرفقات",
                _ => "الضمانات"
            };
        }

        private string BuildGuaranteeHandoffSectionText(GuaranteeFocusArea focusArea, int? requestIdToFocus)
        {
            return focusArea switch
            {
                GuaranteeFocusArea.Requests when requestIdToFocus.HasValue => "حدث الطلب المرتبط داخل السجل الزمني",
                GuaranteeFocusArea.Requests => "السجل الزمني للضمان",
                GuaranteeFocusArea.Outputs => "مخرجات الطلب داخل السجل الزمني",
                GuaranteeFocusArea.Attachments => "مرفقات الضمان في اللوحة الجانبية",
                GuaranteeFocusArea.Actions => "إجراءات الضمان السريعة في المحفظة",
                _ => "الضمان المحدد في المحفظة"
            };
        }

        private void OpenGuaranteeContext()
        {
            if (!TryResolveGuaranteeContextHandoff(
                    out ShellViewModel shell,
                    out GuaranteeRow row,
                    out GuaranteeFocusArea focusArea,
                    out int? requestIdToFocus,
                    out _))
            {
                MessageBox.Show("تعذر تحديد الضمان المرتبط بهذا الجواب.", "الاستعلامات التشغيلية", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Close();
            shell.RouteGuaranteeContext(row, focusArea, requestIdToFocus, "inquiry");
        }

        private void OpenAttachments()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            if (guarantee?.Attachments?.Count > 0)
            {
                AttachmentPickerDialog.ShowFor(
                    guarantee.Attachments,
                    $"inquiry-attachments:{guarantee.RootId ?? guarantee.Id}",
                    $"مرفقات الضمان - {guarantee.GuaranteeNo}");
            }
        }

        private void OpenLetter()
        {
            if (_result.RelatedRequest is not { HasLetter: true } request)
            {
                return;
            }

            _workflow.OpenRequestLetter(request);
        }

        private void OpenResponse()
        {
            if (_result.RelatedRequest is not { HasResponseDocument: true } request)
            {
                return;
            }

            _workflow.OpenResponseDocument(request);
        }

        private void ExportGuaranteeReport()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            if (guarantee == null)
            {
                return;
            }

            bool exported = _excel.ExportSingleGuaranteeReport(guarantee);
            if (!exported)
            {
                return;
            }

            string savedFile = string.IsNullOrWhiteSpace(_excel.LastOutputPath)
                ? "تم حفظ الملف بنجاح"
                : Path.GetFileName(_excel.LastOutputPath);
            App.CurrentApp.GetRequiredService<IShellStatusService>().ShowSuccess(
                $"تم تصدير تقرير الضمان رقم {guarantee.GuaranteeNo}.",
                $"الاستعلامات التشغيلية • {savedFile}");
        }

        private void ConfigureNextStepState()
        {
            Guarantee? guarantee = ResolveFocusGuarantee();
            bool hasAttachments = guarantee?.Attachments?.Count > 0;

            if (TryResolveGuaranteeContextHandoff(
                    out _,
                    out _,
                    out GuaranteeFocusArea focusArea,
                    out int? requestIdToFocus,
                    out bool hasInquiryRoute) &&
                hasInquiryRoute)
            {
                string sectionText = BuildGuaranteeHandoffSectionText(focusArea, requestIdToFocus);
                ApplyNextStepState(
                    $"أقرب خطوة الآن هي فتح {sectionText}.",
                    "هذا يعيدك من الجواب المختصر إلى الوجهة الظاهرة المناسبة بدل فتح نافذة ملف مستقلة.",
                    BuildGuaranteeContextButtonText(focusArea, requestIdToFocus),
                    OpenGuaranteeContext);
                return;
            }

            if (_result.CanOpenResponseDocument)
            {
                ApplyNextStepState(
                    "أقرب خطوة الآن هي مراجعة رد البنك الذي استند إليه هذا الجواب.",
                    "سيفتح المستند النهائي المرتبط بالطلب الحالي حتى تتأكد من النص المرجعي قبل أي متابعة أخرى.",
                    "فتح رد البنك",
                    OpenResponse);
                return;
            }

            if (_result.CanOpenRequestLetter)
            {
                ApplyNextStepState(
                    "أقرب خطوة الآن هي مراجعة خطاب الطلب المرتبط بهذا الجواب.",
                    "سيفتح الخطاب الأصلي المرتبط بالطلب حتى تربط النتيجة الحالية بسياقها التنفيذي المباشر.",
                    "فتح خطاب الطلب",
                    OpenLetter);
                return;
            }

            if (hasAttachments)
            {
                ApplyNextStepState(
                    "إذا احتجت تدقيقًا إضافيًا، فابدأ بمرفقات الإصدار الحالي لهذا الضمان.",
                    "المرفقات تعطيك الدليل العملي الأقرب قبل تصدير التقرير أو الانتقال إلى مساحة العمل المناسبة.",
                    "فتح مرفقات الإصدار",
                    OpenAttachments);
                return;
            }

            if (guarantee != null)
            {
                ApplyNextStepState(
                    "يمكنك الآن حفظ تقرير الضمان الحالي لمراجعته أو مشاركته خارج هذا الحوار.",
                    "هذا مفيد عندما يكون الجواب واضحًا وتريد تثبيت مرجعه كتابيًا بدل التنقل بين الأدلة مرة أخرى.",
                    "تقرير الضمان",
                    ExportGuaranteeReport);
                return;
            }

            ApplyNextStepState(
                "لا توجد خطوة خارجية أوضح من الجواب الحالي.",
                "راجع الأدلة والخط الزمني في نفس الحوار، ثم أغلقه عندما تكتفي بالسياق الحالي.",
                "إغلاق الحوار",
                Close);
        }

        private void ApplyNextStepState(string summary, string hint, string actionLabel, Action action)
        {
            _nextStepSummary.Text = summary;
            _nextStepHint.Text = hint;
            _nextStepActionButton.Content = actionLabel;
            _nextStepAction = action;
        }
    }
}
