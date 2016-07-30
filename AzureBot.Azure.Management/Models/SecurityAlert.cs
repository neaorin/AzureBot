using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureBot.Azure.Management.Models
{
    [Serializable]
    public class SecurityAlert
    {
        public SecurityAlertState State { get; set; }
        public SecurityAlertSeverity Severity { get; set; }
        public string Description { get; set; }
        public DateTime ReportedTime { get; set; }
        public string AssociatedResourceId { get; set; } 
    }

    public enum SecurityAlertState
    {
        Open,
        Resolved
    }

    public enum SecurityAlertSeverity
    {
        Low,
        Medium,
        High
    }
}
