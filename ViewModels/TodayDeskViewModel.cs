using System;
using System.Collections.Generic;
using System.Linq;
using GuaranteeManager.Models;
using GuaranteeManager.Services;

namespace GuaranteeManager.ViewModels
{
    public sealed class TodayDeskViewModel : ViewModelBase
    {
        private readonly IDatabaseService _databaseService;

        private IReadOnlyList<Guarantee> _urgentGuarantees = Array.Empty<Guarantee>();
        private IReadOnlyList<WorkflowRequestListItem> _pendingRequests = Array.Empty<WorkflowRequestListItem>();
        private string _totalGuarantees = "0";
        private string _expiringSoon = "0";
        private string _expiredActive = "0";
        private string _pendingRequestsCount = "0";
        private Guarantee? _selectedUrgentGuarantee;
        private WorkflowRequestListItem? _selectedPendingRequest;

        public TodayDeskViewModel(IDatabaseService databaseService, IWorkflowService workflowService)
        {
            _databaseService = databaseService;
        }

        public IReadOnlyList<Guarantee> UrgentGuarantees
        {
            get => _urgentGuarantees;
            private set => SetProperty(ref _urgentGuarantees, value);
        }

        public IReadOnlyList<WorkflowRequestListItem> PendingRequests
        {
            get => _pendingRequests;
            private set => SetProperty(ref _pendingRequests, value);
        }

        public string TotalGuarantees
        {
            get => _totalGuarantees;
            private set => SetProperty(ref _totalGuarantees, value);
        }

        public string ExpiringSoon
        {
            get => _expiringSoon;
            private set => SetProperty(ref _expiringSoon, value);
        }

        public string ExpiredActive
        {
            get => _expiredActive;
            private set => SetProperty(ref _expiredActive, value);
        }

        public string PendingRequestsCount
        {
            get => _pendingRequestsCount;
            private set => SetProperty(ref _pendingRequestsCount, value);
        }

        public Guarantee? SelectedUrgentGuarantee
        {
            get => _selectedUrgentGuarantee;
            set => SetProperty(ref _selectedUrgentGuarantee, value);
        }

        public WorkflowRequestListItem? SelectedPendingRequest
        {
            get => _selectedPendingRequest;
            set => SetProperty(ref _selectedPendingRequest, value);
        }

        public void Refresh()
        {
            TotalGuarantees = _databaseService.CountGuarantees().ToString();
            ExpiringSoon = _databaseService.CountGuarantees(new GuaranteeQueryOptions
            {
                TimeStatus = GuaranteeTimeStatus.ExpiringSoon,
                IncludeAttachments = false
            }).ToString();
            ExpiredActive = _databaseService.CountGuarantees(new GuaranteeQueryOptions
            {
                LifecycleStatus = GuaranteeLifecycleStatus.Expired,
                IncludeAttachments = false
            }).ToString();
            PendingRequestsCount = _databaseService.GetPendingWorkflowRequestCount().ToString();

            UrgentGuarantees = _databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                UrgentOnly = true,
                Limit = 12,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });

            PendingRequests = _databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                PendingOrMissingResponseOnly = true,
                Limit = 12,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
            });

            SelectedUrgentGuarantee = UrgentGuarantees.FirstOrDefault();
            SelectedPendingRequest = PendingRequests.FirstOrDefault();
        }

        public List<Guarantee> GetGuaranteesByBank(string bank)
        {
            return _databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                Bank = bank,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });
        }

        public List<Guarantee> GetGuaranteesBySupplier(string supplier)
        {
            return _databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                Supplier = supplier,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });
        }

        public List<Guarantee> GetGuaranteesByTemporalStatus(GuaranteeTimeStatus temporalStatus)
        {
            return _databaseService.QueryGuarantees(new GuaranteeQueryOptions
            {
                TimeStatus = temporalStatus,
                SortMode = GuaranteeQuerySortMode.ExpiryDateAscendingThenGuaranteeNo
            });
        }

        public List<WorkflowRequestListItem> GetPendingRequestsByType(RequestType type)
        {
            return _databaseService.QueryWorkflowRequests(new WorkflowRequestQueryOptions
            {
                RequestType = type,
                RequestStatus = RequestStatus.Pending,
                SortMode = WorkflowRequestQuerySortMode.RequestDateAscending
            });
        }
    }
}
