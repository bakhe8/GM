using GuaranteeManager.Models;

namespace GuaranteeManager.Services
{
    internal sealed class WorkflowExecutionContext
    {
        public WorkflowExecutionContext(WorkflowRequest request, Guarantee baseGuarantee, Guarantee currentGuarantee)
        {
            Request = request;
            BaseGuarantee = baseGuarantee;
            CurrentGuarantee = currentGuarantee;
        }

        public WorkflowRequest Request { get; }

        public Guarantee BaseGuarantee { get; }

        public Guarantee CurrentGuarantee { get; }
    }
}
