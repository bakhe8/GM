using System;
using System.Collections.Generic;
using GuaranteeManager.Contracts;
using GuaranteeManager.Models;
using GuaranteeManager.Services;
using GuaranteeManager.ViewModels;
using Xunit;

namespace GuaranteeManager.Tests
{
    public sealed class ShellViewModelTests
    {
        [Fact]
        public void NavigateCommand_UpdatesWorkspaceState()
        {
            FakeDatabaseService database = new();
            FakeViewFactory viewFactory = new();
            ShellViewModel viewModel = new(database, viewFactory);

            viewModel.NavigateCommand.Execute("Portfolio");

            Assert.Same(viewFactory.DataTableView, viewModel.CurrentContent);
            Assert.Equal("Portfolio", viewModel.ActiveNavigationKey);
            Assert.Equal("الضمانات", viewModel.CurrentWorkspaceName);
            Assert.Equal("تم فتح الضمانات.", viewModel.StatusMessage);
        }

        [Fact]
        public void SearchCommand_ShowsGuaranteeFileAndRecordsLastFile()
        {
            Guarantee guarantee = CreateGuarantee(id: 42, guaranteeNo: "GN-42");
            FakeDatabaseService database = new()
            {
                ExactGuarantee = guarantee,
                GuaranteeByIdResult = guarantee
            };
            FakeViewFactory viewFactory = new();
            ShellViewModel viewModel = new(database, viewFactory);
            viewModel.SearchQuery = guarantee.GuaranteeNo;

            viewModel.SearchCommand.Execute(null);

            Assert.Same(viewFactory.GuaranteeFileView, viewModel.CurrentContent);
            Assert.Same(guarantee, viewFactory.GuaranteeFileView.LoadedGuarantee);
            Assert.True(viewModel.HasLastFile);
            Assert.Contains("ملف الضمان GN-42", viewModel.LastFileToolTip);
            Assert.Contains("البحث الموحد", viewModel.LastFileToolTip);
        }

        [Fact]
        public void SearchCommand_RoutesToWorkflowWorkspaceWhenWorkflowResultsExist()
        {
            FakeDatabaseService database = new();
            database.WorkflowResults.Add(new WorkflowRequestListItem
            {
                Request = new WorkflowRequest { Id = 7 }
            });

            FakeViewFactory viewFactory = new();
            ShellViewModel viewModel = new(database, viewFactory)
            {
                SearchQuery = "follow-up"
            };

            viewModel.SearchCommand.Execute(null);

            Assert.Same(viewFactory.OperationCenterView, viewModel.CurrentContent);
            Assert.Equal("follow-up", viewFactory.OperationCenterView.LastSearch);
            Assert.Equal("BankRoom", viewModel.ActiveNavigationKey);
            Assert.Equal("تم توجيه البحث إلى شاشة الطلبات باستخدام العبارة: follow-up", viewModel.StatusMessage);
        }

        [Fact]
        public void ShowOperationCenter_WithFocusedRequest_PassesRequestIdToWorkspace()
        {
            FakeDatabaseService database = new();
            FakeViewFactory viewFactory = new();
            ShellViewModel viewModel = new(database, viewFactory);

            viewModel.ShowOperationCenter(requestIdToFocus: 19);

            Assert.Same(viewFactory.OperationCenterView, viewModel.CurrentContent);
            Assert.Equal(19, viewFactory.OperationCenterView.LastRequestId);
            Assert.Equal(1, viewFactory.OperationCenterView.RefreshCount);
            Assert.Equal("تم فتح الطلبات وتحديد الطلب المطلوب.", viewModel.StatusMessage);
        }

        [Fact]
        public void ResumeLastFileCommand_ReopensStoredGuarantee()
        {
            Guarantee guarantee = CreateGuarantee(id: 81, guaranteeNo: "GN-81");
            FakeDatabaseService database = new()
            {
                ExactGuarantee = guarantee,
                GuaranteeByIdResult = guarantee
            };
            FakeViewFactory viewFactory = new();
            ShellViewModel viewModel = new(database, viewFactory)
            {
                SearchQuery = guarantee.GuaranteeNo
            };

            viewModel.SearchCommand.Execute(null);
            viewModel.NavigateCommand.Execute("Today");
            viewModel.ResumeLastFileCommand.Execute(null);

            Assert.Same(viewFactory.GuaranteeFileView, viewModel.CurrentContent);
            Assert.Equal(2, viewFactory.GuaranteeFileView.LoadCount);
            Assert.Contains("آخر ملف", viewModel.LastFileToolTip);
        }

        [Fact]
        public void NavigateCommand_DoesNotChangeWorkspaceWhenCurrentContentBlocksNavigation()
        {
            FakeDatabaseService database = new();
            FakeViewFactory viewFactory = new()
            {
                AddEntryFactory = _ => new GuardedAddEntryView(allowNavigation: false)
            };
            ShellViewModel viewModel = new(database, viewFactory);
            object? initialContent = viewModel.CurrentContent;

            viewModel.NavigateCommand.Execute("Portfolio");

            Assert.Same(initialContent, viewModel.CurrentContent);
            Assert.Equal("Today", viewModel.ActiveNavigationKey);
            Assert.Equal("تم إلغاء التنقل بسبب وجود تغييرات غير محفوظة.", viewModel.StatusMessage);
            Assert.Equal(ShellStatusTone.Warning, viewModel.StatusTone);
        }

        private static Guarantee CreateGuarantee(int id, string guaranteeNo)
        {
            return new Guarantee
            {
                Id = id,
                GuaranteeNo = guaranteeNo,
                Supplier = $"Supplier-{id}",
                Bank = $"Bank-{id}",
                ExpiryDate = DateTime.Today.AddDays(30)
            };
        }

        private sealed class FakeViewFactory : IViewFactory
        {
            public TestRefreshableView TodayDeskView { get; } = new();
            public TestSearchableView DataTableView { get; } = new();
            public TestOperationCenterView OperationCenterView { get; } = new();
            public TestRefreshableView SettingsView { get; } = new();
            public TestGuaranteeFileView GuaranteeFileView { get; } = new();

            public Func<GuaranteeFormReturnTarget, object> AddEntryFactory { get; set; } = target => new TestAddEntryView(target);

            public IRefreshableView CreateTodayDesk() => TodayDeskView;

            public IShellSearchableView CreateDataTable() => DataTableView;

            public IOperationCenterWorkspace CreateOperationCenter() => OperationCenterView;

            public IRefreshableView CreateSettings() => SettingsView;

            public object CreateAddEntry(Guarantee? editGuarantee = null, GuaranteeFormReturnTarget returnTarget = GuaranteeFormReturnTarget.DataTable)
            {
                return AddEntryFactory(returnTarget);
            }

            public IGuaranteeFileWorkspace CreateGuaranteeFile() => GuaranteeFileView;
        }

        private class TestRefreshableView : IRefreshableView
        {
            public int RefreshCount { get; private set; }

            public void RefreshView()
            {
                RefreshCount++;
            }
        }

        private class TestSearchableView : TestRefreshableView, IShellSearchableView
        {
            public string? LastSearch { get; private set; }

            public void ApplyShellSearch(string query)
            {
                LastSearch = query;
            }
        }

        private sealed class TestOperationCenterView : TestSearchableView, IOperationCenterWorkspace
        {
            public int? LastRequestId { get; private set; }

            public void SetRequestFocus(int? requestId)
            {
                LastRequestId = requestId;
            }
        }

        private sealed class TestGuaranteeFileView : TestRefreshableView, IGuaranteeFileWorkspace
        {
            public Guarantee? LoadedGuarantee { get; private set; }

            public bool LastUserInitiated { get; private set; }

            public int? LastRequestId { get; private set; }

            public GuaranteeFileFocusArea LastFocusArea { get; private set; }

            public int LoadCount { get; private set; }

            public void SetRequestFocus(int? requestId)
            {
                LastRequestId = requestId;
            }

            public void LoadGuarantee(Guarantee guarantee, bool userInitiated = false)
            {
                LoadedGuarantee = guarantee;
                LastUserInitiated = userInitiated;
                LoadCount++;
            }

            public void FocusSection(GuaranteeFileFocusArea area)
            {
                LastFocusArea = area;
            }
        }

        private sealed class TestAddEntryView
        {
            public TestAddEntryView(GuaranteeFormReturnTarget returnTarget)
            {
                ReturnTarget = returnTarget;
            }

            public GuaranteeFormReturnTarget ReturnTarget { get; }
        }

        private sealed class GuardedAddEntryView : INavigationGuard
        {
            private readonly bool _allowNavigation;

            public GuardedAddEntryView(bool allowNavigation)
            {
                _allowNavigation = allowNavigation;
            }

            public bool HasUnsavedChanges => true;

            public bool ConfirmNavigationAway()
            {
                return _allowNavigation;
            }
        }

        private sealed class FakeDatabaseService : DatabaseServiceStub
        {
            public Guarantee? ExactGuarantee { get; set; }

            public Guarantee? GuaranteeByIdResult { get; set; }

            public List<Guarantee> GuaranteeResults { get; } = new();

            public List<WorkflowRequestListItem> WorkflowResults { get; } = new();

            public GuaranteeQueryOptions? LastGuaranteeQuery { get; private set; }

            public WorkflowRequestQueryOptions? LastWorkflowQuery { get; private set; }

            public override Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo)
            {
                return ExactGuarantee != null && string.Equals(ExactGuarantee.GuaranteeNo, guaranteeNo, StringComparison.Ordinal)
                    ? ExactGuarantee
                    : null;
            }

            public override List<Guarantee> QueryGuarantees(GuaranteeQueryOptions options)
            {
                LastGuaranteeQuery = options;
                return new List<Guarantee>(GuaranteeResults);
            }

            public override List<WorkflowRequestListItem> QueryWorkflowRequests(WorkflowRequestQueryOptions options)
            {
                LastWorkflowQuery = options;
                return new List<WorkflowRequestListItem>(WorkflowResults);
            }

            public override Guarantee? GetGuaranteeById(int guaranteeId)
            {
                return GuaranteeByIdResult != null && GuaranteeByIdResult.Id == guaranteeId
                    ? GuaranteeByIdResult
                    : null;
            }
        }

        private abstract class DatabaseServiceStub : IDatabaseService
        {
            public virtual void SaveGuarantee(Guarantee g, List<string> tempFilePaths) => throw new NotSupportedException();
            public virtual int UpdateGuarantee(Guarantee g, List<string> newTempFiles, List<AttachmentRecord> removedAttachments) => throw new NotSupportedException();
            public virtual List<Guarantee> QueryGuarantees(GuaranteeQueryOptions options) => throw new NotSupportedException();
            public virtual int CountGuarantees(GuaranteeQueryOptions? options = null) => throw new NotSupportedException();
            public virtual int CountAttachments() => throw new NotSupportedException();
            public virtual List<Guarantee> SearchGuarantees(string query) => throw new NotSupportedException();
            public virtual List<Guarantee> GetGuaranteeHistory(int guaranteeId) => throw new NotSupportedException();
            public virtual int SaveWorkflowRequest(WorkflowRequest req) => throw new NotSupportedException();
            public virtual bool HasPendingWorkflowRequest(int rootId, RequestType requestType) => throw new NotSupportedException();
            public virtual int GetPendingWorkflowRequestCount() => throw new NotSupportedException();
            public virtual WorkflowRequest? GetWorkflowRequestById(int requestId) => throw new NotSupportedException();
            public virtual List<WorkflowRequest> GetWorkflowRequestsByRootId(int rootId) => throw new NotSupportedException();
            public virtual List<WorkflowRequestListItem> QueryWorkflowRequests(WorkflowRequestQueryOptions options) => throw new NotSupportedException();
            public virtual int CountWorkflowRequests(WorkflowRequestQueryOptions? options = null) => throw new NotSupportedException();
            public virtual List<WorkflowRequestListItem> SearchWorkflowRequests(string query) => throw new NotSupportedException();
            public virtual void RecordWorkflowResponse(int requestId, RequestStatus newStatus, string responseNotes, string responseOriginalFileName, string responseSavedFileName, int? resultVersionId = null) => throw new NotSupportedException();
            public virtual void AttachWorkflowResponseDocument(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName) => throw new NotSupportedException();
            public virtual int ExecuteExtensionWorkflowRequest(int requestId, DateTime newExpiryDate, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public virtual int ExecuteReductionWorkflowRequest(int requestId, decimal newAmount, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public virtual int ExecuteReleaseWorkflowRequest(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public virtual int ExecuteLiquidationWorkflowRequest(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public virtual int? ExecuteVerificationWorkflowRequest(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null, bool promoteResponseDocumentToOfficialAttachment = false) => throw new NotSupportedException();
            public virtual int ExecuteAnnulmentWorkflowRequest(int requestId, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public virtual int ExecuteReplacementWorkflowRequest(int requestId, string replacementGuaranteeNo, string replacementSupplier, string replacementBank, decimal replacementAmount, DateTime replacementExpiryDate, string replacementGuaranteeType, string replacementBeneficiary, GuaranteeReferenceType replacementReferenceType, string replacementReferenceNumber, string responseNotes, string responseOriginalFileName, string responseSavedFileName, string? responseAttachmentSourcePath = null) => throw new NotSupportedException();
            public virtual void DeleteAttachment(AttachmentRecord att) => throw new NotSupportedException();
            public virtual List<string> GetUniqueValues(string columnName) => throw new NotSupportedException();
            public virtual bool IsGuaranteeNoUnique(string guaranteeNo) => throw new NotSupportedException();
            public virtual Guarantee? GetGuaranteeById(int guaranteeId) => throw new NotSupportedException();
            public virtual Guarantee? GetCurrentGuaranteeByRootId(int rootId) => throw new NotSupportedException();
            public virtual Guarantee? GetCurrentGuaranteeByNo(string guaranteeNo) => throw new NotSupportedException();
            public virtual int CreateNewVersion(Guarantee newG, int sourceId, List<string> newTempFiles, List<AttachmentRecord> inheritedAttachments) => throw new NotSupportedException();
        }
    }
}
