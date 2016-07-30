using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureBot.Azure.Management.Models
{
    [Serializable]
    public class SecurityTask
    {
        public SecurityTaskState State { get; set; }
        public SecurityTaskSeverity Severity { get; set; }
        public string Name { get; set; }
        public DateTime Created { get; set; }

        public virtual string FullName { get { return Name; } }

        public override string ToString()
        {
            return $"*[{Severity}]*  {FullName}";
        }
    }

    [Serializable]
    public class AzureSqlServerSecurityTask : SecurityTask
    {
        public string ServerName { get; set; }
        public string ServerId { get; set; }

        public override string FullName
        {
            get
            {
                return $"{Name} {ServerName}";
            }
        }

        public override string ToString()
        {
            return $"*[{Severity}]*  {Name} **{ServerName}**";
        }
    }

    [Serializable]
    public class AzureSqlDatabaseSecurityTask : AzureSqlServerSecurityTask
    {
        public string DatabaseName { get; set; }
        public string DatabaseId { get; set; }

        public override string FullName
        {
            get
            {
                return $"{Name} {DatabaseName} on server {ServerName}";
            }
        }

        public override string ToString()
        {
            return $"*[{Severity}]*  {Name} **{DatabaseName}** on server **{ServerName}**";
        }
    }

    public enum SecurityTaskState
    {
        Active,
        Resolved
    }

    public enum SecurityTaskSeverity
    {
        Low,
        Medium,
        High
    }
}
