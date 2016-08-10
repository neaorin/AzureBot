﻿namespace AzureBot.Azure.Management.ResourceManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.Automation;
    using Microsoft.Azure.Management.Compute;
    using Microsoft.Azure.Subscriptions;
    using Models;
    using AzureModels = Microsoft.Azure.Management.Automation.Models;
    using TokenCredentials = Microsoft.Azure.TokenCloudCredentials;
    using System.Net;
    using Newtonsoft.Json;
    using Microsoft.Azure.Management.Sql;
    using Microsoft.Azure.Management.Sql.Models;

    public class AzureRepository
    {
        public async Task<IEnumerable<Subscription>> ListSubscriptionsAsync(string accessToken)
        {
            var credentials = new TokenCredentials(accessToken);

            using (SubscriptionClient client = new SubscriptionClient(credentials))
            {
                var subscriptionsResult = await client.Subscriptions.ListAsync().ConfigureAwait(false);
                var subscriptions = subscriptionsResult.Subscriptions.OrderBy(x => x.DisplayName).Select(sub => new Subscription { SubscriptionId = sub.SubscriptionId, DisplayName = sub.DisplayName }).ToList();
                return subscriptions;
            }
        }

        public async Task<Subscription> GetSubscription(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(accessToken);

            using (SubscriptionClient client = new SubscriptionClient(credentials))
            {
                var subscriptionsResult = await client.Subscriptions.GetAsync(subscriptionId, CancellationToken.None);
                return new Subscription
                {
                    SubscriptionId = subscriptionsResult.Subscription.SubscriptionId,
                    DisplayName = subscriptionsResult.Subscription.DisplayName
                };
            }
        }

        public async Task<IEnumerable<VirtualMachine>> ListVirtualMachinesAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var virtualMachinesResult = await client.VirtualMachines.ListAllAsync(null).ConfigureAwait(false);
                var all = virtualMachinesResult.VirtualMachines.Select(async (vm) =>
                {
                    var resourceGroupName = GetResourceGroup(vm.Id);
                    var response = await client.VirtualMachines.GetWithInstanceViewAsync(resourceGroupName, vm.Name);
                    var vmStatus = response.VirtualMachine.InstanceView.Statuses.Where(p => p.Code.ToLower().StartsWith("powerstate/")).FirstOrDefault();
                    return new VirtualMachine
                    {
                        SubscriptionId = subscriptionId,
                        ResourceGroup = resourceGroupName,
                        Name = vm.Name,
                        PowerState = GetVirtualMachinePowerState(vmStatus?.Code.ToLower() ?? VirtualMachinePowerState.Unknown.ToString()),
                        Size = response.VirtualMachine.HardwareProfile.VirtualMachineSize
                    };
                });

                return await Task.WhenAll(all.ToList());
            }
        }

        public async Task<IEnumerable<AutomationAccount>> ListAutomationAccountsAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationAccountsResult = await automationClient.AutomationAccounts.ListAsync(null).ConfigureAwait(false);
                return automationAccountsResult.AutomationAccounts.Select(account => new AutomationAccount
                {
                    SubscriptionId = subscriptionId,
                    ResourceGroup = GetResourceGroup(account.Id),
                    AutomationAccountId = account.Id,
                    AutomationAccountName = account.Name,
                }).ToList();
            }
        }

        public async Task<IEnumerable<AutomationAccount>> ListRunbooksAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationAccountsResult = await this.ListAutomationAccountsAsync(accessToken, subscriptionId).ConfigureAwait(false);
                var automationAccounts = await Task.WhenAll(
                    automationAccountsResult.Select(
                        async account =>
                        {
                            account.Runbooks = await this.ListAutomationRunbooks(accessToken, subscriptionId, account.ResourceGroup, account.AutomationAccountName);

                            return account;
                        }).ToList());
                return automationAccounts;
            }
        }

        public async Task<IEnumerable<AutomationAccount>> ListRunbookJobsAsync(string accessToken, string subscriptionId)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationAccountsResult = await this.ListAutomationAccountsAsync(accessToken, subscriptionId).ConfigureAwait(false);
                var automationAccounts = await Task.WhenAll(
                    automationAccountsResult.Select(
                        async account =>
                        {
                            account.RunbookJobs = await this.ListAutomationJobs(accessToken, subscriptionId, account.ResourceGroup, account.AutomationAccountName);

                            return account;
                        }).ToList());
                return automationAccounts;
            }
        }

        public async Task<bool> StartVirtualMachineAsync(string accessToken, string subscriptionId, string resourceGroupName, string virtualMachineName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var status = await client.VirtualMachines.StartAsync(resourceGroupName, virtualMachineName).ConfigureAwait(false);
                return status.Status != Microsoft.Azure.Management.Compute.Models.ComputeOperationStatus.Failed;
            }
        }

        public async Task<bool> PowerOffVirtualMachineAsync(string accessToken, string subscriptionId, string resourceGroupName, string virtualMachineName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var status = await client.VirtualMachines.PowerOffAsync(resourceGroupName, virtualMachineName).ConfigureAwait(false);
                return status.Status != Microsoft.Azure.Management.Compute.Models.ComputeOperationStatus.Failed;
            }
        }

        public async Task<bool> DeallocateVirtualMachineAsync(string accessToken, string subscriptionId, string resourceGroupName, string virtualMachineName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);
            using (var client = new ComputeManagementClient(credentials))
            {
                var status = await client.VirtualMachines.DeallocateAsync(resourceGroupName, virtualMachineName).ConfigureAwait(false);
                return status.Status != Microsoft.Azure.Management.Compute.Models.ComputeOperationStatus.Failed;
            }
        }

        public async Task<RunbookJob> StartRunbookAsync(
            string accessToken,
            string subscriptionId,
            string resourceGroupName,
            string automationAccountName,
            string runbookName,
            IDictionary<string, string> runbookParameters = null)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var client = new AutomationManagementClient(credentials))
            {
                var parameters = new AzureModels.JobCreateParameters(
                    new AzureModels.JobCreateProperties(
                        new AzureModels.RunbookAssociationProperty
                        {
                            Name = runbookName
                        })
                    {
                        Parameters = runbookParameters
                    });

                var jobCreateResult = await client.Jobs.CreateAsync(resourceGroupName, automationAccountName, parameters).ConfigureAwait(false);
                return new RunbookJob
                {
                    JobId = jobCreateResult.Job.Properties.JobId.ToString(),
                    StartDateTime = jobCreateResult.Job.Properties.StartTime,
                    EndDateTime = jobCreateResult.Job.Properties.EndTime,
                    Status = jobCreateResult.Job.Properties.Status,
                    ResourceGroupName = resourceGroupName,
                    AutomationAccountName = automationAccountName,
                    RunbookName = runbookName
                };
            }
        }

        public async Task<RunbookJob> GetAutomationJobAsync(string accessToken, string subscriptionId, string resourceGroupName, string automationAccountName, string jobId, bool configureAwait = false)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationJobResult = configureAwait ? await automationClient.Jobs.GetAsync(resourceGroupName, automationAccountName, new Guid(jobId)).ConfigureAwait(false) :
                    await automationClient.Jobs.GetAsync(resourceGroupName, automationAccountName, new Guid(jobId));

                var automationJob = new RunbookJob
                {
                    JobId = automationJobResult.Job.Properties.JobId.ToString(),
                    Status = automationJobResult.Job.Properties.Status,
                    RunbookName = automationJobResult.Job.Properties.Runbook?.Name ?? "_(Unknown)_",
                    ResourceGroupName = resourceGroupName,
                    AutomationAccountName = automationAccountName,
                    StartDateTime = automationJobResult.Job.Properties.StartTime,
                    EndDateTime = automationJobResult.Job.Properties.EndTime
                };

                return automationJob;
            }
        }

        public async Task<string> GetAutomationJobOutputAsync(string accessToken, string subscriptionId, string resourceGroupName, string automationAccountName, string jobId)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var jobOutputResponse = await automationClient.Jobs.GetOutputAsync(resourceGroupName, automationAccountName, new Guid(jobId));

                return jobOutputResponse.Output;
            }
        }

        public async Task<string> GetAutomationRunbookDescriptionAsync(
            string accessToken,
            string subscriptionId,
            string resourceGroupName,
            string automationAccountName,
            string runbookName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationRunbookResult = await automationClient.Runbooks.GetAsync(resourceGroupName, automationAccountName, runbookName);

                return automationRunbookResult.Runbook.Properties.Description;
            }
        }

        public async Task<IEnumerable<SecurityTask>> ListSecurityTasksAsync(string accessToken, string subscriptionId)
        {
            HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create($"https://management.azure.com/subscriptions/{subscriptionId}/providers/microsoft.Security/tasks?api-version=2015-06-01-preview");
            webRequest.Headers.Add("Authorization", $"Bearer {accessToken}");
            webRequest.Accept = "application/json";
            WebResponse webResp = await webRequest.GetResponseAsync();
            string responseText = String.Empty;
            using (var reader = new System.IO.StreamReader(webResp.GetResponseStream()))
            {
                responseText = reader.ReadToEnd();
            }

            var tasks = new List<SecurityTask>();

            dynamic obj = JsonConvert.DeserializeObject(responseText);
            foreach (dynamic taskobj in obj.value)
            {
                dynamic secparams = taskobj?.properties?.securityTaskParameters;
                SecurityTask task;
                if (secparams?.databaseId != null)
                {
                    task = new AzureSqlDatabaseSecurityTask()
                    {
                        DatabaseId = secparams.databaseId,
                        DatabaseName = secparams.databaseName,
                        ServerName = secparams.serverName,
                        Severity = SecurityTaskSeverity.High
                    };
                }
                else if (secparams?.serverId != null)
                    {
                        task = new AzureSqlServerSecurityTask()
                        {
                            ServerId = secparams.serverId,
                            ServerName = secparams.serverName,
                            Severity = SecurityTaskSeverity.High
                        };
                    }
                else
                {
                    task = new SecurityTask();
                    task.Severity = SecurityTaskSeverity.Medium;
                }

                task.Created = Convert.ToDateTime(taskobj?.properties?.creationTimeUtc);
                task.Name = secparams.name;              

                tasks.Add(task);
            }
            return tasks;
        }

        public async Task<bool> FixSecurityTaskAsync(string accessToken, string subscriptionId, SecurityTask task)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            if (task is AzureSqlServerSecurityTask)
            {
                var dbTask = task as AzureSqlServerSecurityTask;
                using (var sqlMgmtClient = new SqlManagementClient(credentials))
                {
                    var response = await sqlMgmtClient.BlobAuditing.CreateOrUpdateServerPolicyAsync(
                        GetResourceGroup(dbTask.ServerId),
                        dbTask.ServerName,
                        new BlobAuditingCreateOrUpdateParameters()
                        {
                            Properties = new BlobAuditingProperties()
                            {
                                State = "Enabled"
                            }
                        });

                    return response.StatusCode == HttpStatusCode.OK;
                }

            }

            else if (task is AzureSqlDatabaseSecurityTask)
            {
                var dbTask = task as AzureSqlDatabaseSecurityTask;
                using (var sqlMgmtClient = new SqlManagementClient(credentials))
                {
                    var response = await sqlMgmtClient.BlobAuditing.CreateOrUpdateDatabasePolicyAsync(
                        GetResourceGroup(dbTask.DatabaseId),
                        dbTask.ServerName,
                        dbTask.DatabaseName,
                        new BlobAuditingCreateOrUpdateParameters()
                        {
                            Properties = new BlobAuditingProperties()
                            {
                                State = "Enabled"                                
                            }
                        });

                    return response.StatusCode == HttpStatusCode.OK;
                }

            }

            return false;
        }

        private static string GetResourceGroup(string id)
        {
            var segments = id.Split('/');
            var resourceGroupName = segments.SkipWhile(segment => segment != "resourceGroups").ElementAtOrDefault(1);
            return resourceGroupName;
        }

        private async Task<IEnumerable<Runbook>> ListAutomationRunbooks(string accessToken, string subscriptionId, string resourceGroupName, string automationAccountName, params string[] runbooksStateFilter)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationRunbooksResult = await automationClient.Runbooks.ListAsync(resourceGroupName, automationAccountName);

                var automationRunbooks = await Task.WhenAll(automationRunbooksResult.Runbooks.Where(x => (runbooksStateFilter == null || !runbooksStateFilter.Any()) || runbooksStateFilter.Contains(x.Properties.State, StringComparer.InvariantCultureIgnoreCase))
                    .Select(async runbook => new Runbook
                    {
                        RunbookId = runbook.Id,
                        RunbookName = runbook.Name,
                        RunbookState = runbook.Properties.State,
                        RunbookParameters = await this.ListAutomationRunbookParameters(accessToken, subscriptionId, resourceGroupName, automationAccountName, runbook.Name)
                    }).ToList());

                return automationRunbooks;
            }
        }

        private async Task<IEnumerable<RunbookJob>> ListAutomationJobs(string accessToken, string subscriptionId, string resourceGroupName, string automationAccountName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationJobsResult = await automationClient.Jobs.ListAsync(resourceGroupName, automationAccountName, parameters: null);

                var automationJobs = automationJobsResult.Jobs.Select(
                     job => new RunbookJob
                     {
                         JobId = job.Properties.JobId.ToString(),
                         Status = job.Properties.Status,
                         RunbookName = job.Properties.Runbook?.Name ?? "_(Unknown)_"
                     }).ToList();

                return automationJobs;
            }
        }

        private async Task<IEnumerable<RunbookParameter>> ListAutomationRunbookParameters(
            string accessToken,
            string subscriptionId,
            string resourceGroupName,
            string automationAccountName,
            string runbookName)
        {
            var credentials = new TokenCredentials(subscriptionId, accessToken);

            using (var automationClient = new AutomationManagementClient(credentials))
            {
                var automationRunbookResult = await automationClient.Runbooks.GetAsync(resourceGroupName, automationAccountName, runbookName);

                var automationRunbookPrameters = automationRunbookResult.Runbook.Properties.Parameters.Select(
                    parameter => new RunbookParameter
                    {
                        ParameterName = parameter.Key,
                        DefaultValue = parameter.Value.DefaultValue,
                        IsMandatory = parameter.Value.IsMandatory,
                        Position = parameter.Value.Position,
                        Type = parameter.Value.Type
                    }).ToList();

                return automationRunbookPrameters;
            }
        }

        private VirtualMachinePowerState GetVirtualMachinePowerState(string code)
        {
            string[] powerStateElements = code.Split('/');

            if (powerStateElements.Length != 2)
            {
                return VirtualMachinePowerState.Unknown;
            }

            var status = powerStateElements[1];

            VirtualMachinePowerState powerState;

            if (!Enum.TryParse(status, true, out powerState))
            {
                return VirtualMachinePowerState.Unknown;
            }

            return powerState;
        }
    }
}