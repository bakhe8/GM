using System.Collections.Generic;
using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    public interface IContextActionService
    {
        IReadOnlyList<ContextActionSection> GetGuaranteeActions();
        IReadOnlyList<ContextActionSection> GetWorkflowRequestActions();
    }
}
