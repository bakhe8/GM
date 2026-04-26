using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public interface IGuaranteeHistoryDocumentService
    {
        string? LastOutputPath { get; }

        bool ExportHistoryToExcel(Guarantee guarantee, IReadOnlyList<Guarantee> history, IReadOnlyList<WorkflowRequest> requests);
        bool PrintHistory(Guarantee guarantee, IReadOnlyList<Guarantee> history, IReadOnlyList<WorkflowRequest> requests);
    }
}
