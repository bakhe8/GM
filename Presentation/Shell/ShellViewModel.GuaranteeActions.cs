using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using GuaranteeManager.Models;
using GuaranteeManager.Utils;
using MessageBox = GuaranteeManager.Services.AppMessageBox;

namespace GuaranteeManager
{
    public sealed partial class ShellViewModel
    {
        private void CreateNewGuarantee()
        {
            _guaranteeWorkspace.CreateNewGuarantee();
        }

        private void EditGuarantee(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.EditGuarantee);
        }

        private void CreateExtensionRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateExtensionRequest);
        }

        private void CreateReleaseRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateReleaseRequest);
        }

        private void CreateReductionRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateReductionRequest);
        }

        private void CreateLiquidationRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateLiquidationRequest);
        }

        private void CreateVerificationRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateVerificationRequest);
        }

        private void CreateReplacementRequest(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CreateReplacementRequest);
        }

        private void RegisterBankResponse(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.RegisterBankResponse);
        }

        private void OpenAttachment(AttachmentItem? item)
        {
            _sessionCoordinator.OpenAttachment(item);
        }

        private void OpenTimelineEvidence(TimelineItem? item)
        {
            if (item == null)
            {
                return;
            }

            switch (item.EvidenceActionKind)
            {
                case TimelineEvidenceActionKind.Attachment:
                    if (item.EvidenceAttachment != null)
                    {
                        _sessionCoordinator.OpenAttachment(AttachmentItem.FromAttachment(item.EvidenceAttachment));
                    }

                    break;
                case TimelineEvidenceActionKind.RequestLetter:
                    if (item.EvidenceRequest != null)
                    {
                        _guaranteeWorkspace.OpenRequestLetter(item.EvidenceRequest);
                    }

                    break;
                case TimelineEvidenceActionKind.ResponseDocument:
                    if (item.EvidenceRequest != null)
                    {
                        if (item.EvidenceRequest.HasResponseDocument)
                        {
                            _guaranteeWorkspace.OpenResponseDocument(item.EvidenceRequest);
                        }
                        else
                        {
                            _guaranteeWorkspace.AttachResponseDocument(
                                item.EvidenceRequest,
                                SelectedGuarantee?.GuaranteeNo ?? string.Empty);
                        }
                    }

                    break;
                case TimelineEvidenceActionKind.OfficialAttachment:
                    ExecuteGuaranteeAction(
                        SelectedGuarantee,
                        target => _guaranteeWorkspace.AttachTimelineEvidence(target, item),
                        syncSelection: true);
                    break;
            }
        }

        private void ShowAllAttachments()
        {
            ExecuteGuaranteeAction(SelectedGuarantee, target => _guaranteeWorkspace.ShowAttachments(target, showEmptyMessage: true));
        }

        private void OpenRequestPreview(GuaranteeRequestPreviewItem? item)
        {
            if (item == null)
            {
                return;
            }

            FocusGuaranteeSection(GuaranteeFocusArea.Requests, item.Request.Id);
        }

        private void RegisterRequestPreviewResponse(GuaranteeRequestPreviewItem? item)
        {
            if (item?.CanRegisterResponse != true || SelectedGuarantee == null)
            {
                return;
            }

            _guaranteeWorkspace.RegisterBankResponse(item.Request, SelectedGuarantee.GuaranteeNo);
        }

        private void OpenRequestPreviewLetter(GuaranteeRequestPreviewItem? item)
        {
            if (item != null)
            {
                _guaranteeWorkspace.OpenRequestLetter(item.Request);
            }
        }

        private void OpenRequestPreviewResponse(GuaranteeRequestPreviewItem? item)
        {
            if (item != null)
            {
                _guaranteeWorkspace.OpenResponseDocument(item.Request);
            }
        }

        private void FocusSuggestedGuaranteeArea()
        {
            if (SelectedGuarantee?.HasSuggestedFocus == true)
            {
                FocusGuaranteeSection(SelectedGuarantee.SuggestedFocusArea);
            }
        }

        private static string FormatOfficialAttachmentCount(int count)
        {
            return count switch
            {
                1 => "مرفق رسمي واحد",
                2 => "مرفقان رسميان",
                _ => $"{count.ToString("N0", CultureInfo.InvariantCulture)} مرفقات رسمية"
            };
        }

        private void CopyGuaranteeNo(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyGuaranteeNo, syncSelection: true);
        }

        private void CopyGuaranteeSupplier(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopySupplier, syncSelection: true);
        }

        private void CopyGuaranteeReferenceType(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyReferenceType, syncSelection: true);
        }

        private void CopyGuaranteeReferenceNumber(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyReferenceNumber, syncSelection: true);
        }

        private void CopyGuaranteeType(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyGuaranteeType, syncSelection: true);
        }

        private void CopyGuaranteeIssueDate(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyIssueDate, syncSelection: true);
        }

        private void CopyGuaranteeExpiryDate(GuaranteeRow? row)
        {
            ExecuteGuaranteeAction(row, _guaranteeWorkspace.CopyExpiryDate, syncSelection: true);
        }

        public void FocusGuaranteeSection(GuaranteeFocusArea area, int? requestIdToFocus = null)
        {
            if (SelectedGuarantee == null)
            {
                return;
            }

            _currentGuaranteeFocusArea = area;
            int? nextFocusedRequestId = area == GuaranteeFocusArea.Requests ? requestIdToFocus : null;
            _guaranteeFocusRequestVersion++;
            if (_focusedGuaranteeRequestId != nextFocusedRequestId)
            {
                _focusedGuaranteeRequestId = nextFocusedRequestId;
                RaiseGuaranteeContextSectionTextProperties();
                RefreshSelectedGuaranteeArtifacts();
            }

            GuaranteeFocusRequested?.Invoke(area, requestIdToFocus);
        }

        private void OpenOutputLetter(GuaranteeOutputPreviewItem? item)
        {
            if (item != null)
            {
                _guaranteeWorkspace.OpenRequestLetter(item.Request);
            }
        }

        private void OpenOutputResponse(GuaranteeOutputPreviewItem? item)
        {
            if (item != null)
            {
                _guaranteeWorkspace.OpenResponseDocument(item.Request);
            }
        }

        private void ExecuteGuaranteeAction(GuaranteeRow? row, Action<GuaranteeRow> action, bool syncSelection = false)
        {
            GuaranteeRow? target = ResolveTarget(row);
            if (target == null)
            {
                return;
            }

            if (syncSelection)
            {
                SelectedGuarantee = target;
            }

            action(target);
        }

        private GuaranteeRow? ResolveTarget(GuaranteeRow? row)
        {
            GuaranteeRow? target = row ?? SelectedGuarantee;
            if (target == null)
            {
                MessageBox.Show("اختر ضماناً أولاً.", "إجراء الضمان", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            return target;
        }
    }
}
