namespace AzureBot.Forms
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Azure.Management.Models;

    [Serializable]
    public class SecurityTaskFormState
    {
        public SecurityTaskFormState(IEnumerable<SecurityTask> availableTasks)
        {
            this.AvailableTasks = availableTasks;
        }

        public string TaskFullName { get; set; }

        public IEnumerable<SecurityTask> AvailableTasks { get; private set; }

        public SecurityTask SelectedTask
        {
            get
            {
                return this.AvailableTasks.Where(p => p.FullName == this.TaskFullName).SingleOrDefault();
            }
        }
    }
}