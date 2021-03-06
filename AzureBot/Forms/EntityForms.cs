﻿namespace AzureBot.Forms
{
    using System;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.Bot.Builder.FormFlow;
    using Microsoft.Bot.Builder.FormFlow.Advanced;
    using Azure.Management.Models;

    public class EntityForms
    {
        public static IForm<SubscriptionFormState> BuildSubscriptionForm()
        {
            var prompt = new PromptAttribute()
            {
                ChoiceStyle = ChoiceStyleOptions.PerLine
            };

            return CreateCustomForm<SubscriptionFormState>()
                .Field(new FieldReflector<SubscriptionFormState>(nameof(SubscriptionFormState.SubscriptionId))
                .SetType(null)
                .SetActive(x => x.AvailableSubscriptions.Any())
                .SetPrompt(PerLinePromptAttribute("Please select the subscription you want to work with: {||}"))
                .SetDefine((state, field) =>
                {
                    foreach (var sub in state.AvailableSubscriptions)
                    {
                        field.AddDescription(sub.SubscriptionId, sub.DisplayName)
                            .AddTerms(sub.SubscriptionId, sub.DisplayName);
                    }

                    return Task.FromResult(true);
                }))
               .Build();
        }

        public static IForm<VirtualMachineFormState> BuildVirtualMachinesForm()
        {
            return CreateCustomForm<VirtualMachineFormState>()
                .Field(nameof(VirtualMachineFormState.Operation), (state) => false)
                .Field(new FieldReflector<VirtualMachineFormState>(nameof(VirtualMachineFormState.VirtualMachine))
                .SetType(null)
                .SetPrompt(PerLinePromptAttribute("Please select the virtual machine you want to {Operation}: {||}"))
                .SetDefine((state, field) =>
                {
                    foreach (var vm in state.AvailableVMs)
                    {
                        field
                            .AddDescription(vm.Name, vm.ToString())
                            .AddTerms(vm.Name, vm.Name);
                    }

                    return Task.FromResult(true);
                }))
               .Confirm("Would you like to {Operation} virtual machine '{VirtualMachine}'?", (state) => (Operations)Enum.Parse(typeof(Operations), state.Operation, true) == Operations.Start, null)
               .Confirm("Would you like to {Operation} virtual machine '{VirtualMachine}'? Please note that your VM will still incur compute charges.", (state) => (Operations)Enum.Parse(typeof(Operations), state.Operation, true) == Operations.Shutdown, null)
               .Confirm("Would you like to {Operation} virtual machine '{VirtualMachine}'? Your VM won't incur charges and all IP addresses will be released.", (state) => (Operations)Enum.Parse(typeof(Operations), state.Operation, true) == Operations.Stop, null)
               .Build();
        }

        public static IForm<AllVirtualMachinesFormState> BuildAllVirtualMachinesForm()
        {
            return CreateCustomForm<AllVirtualMachinesFormState>()
                .Field(nameof(AllVirtualMachinesFormState.Operation), (state) => false)
                .Field(nameof(AllVirtualMachinesFormState.VirtualMachines), (state) => false)
               .Confirm("You are trying to {Operation} the following virtual machines: {VirtualMachines} Are you sure?", (state) => (Operations)Enum.Parse(typeof(Operations), state.Operation, true) == Operations.Start, null)
               .Confirm("You are trying to {Operation} the following virtual machines: {VirtualMachines} Are you sure? Please note that your VMs will still incur compute charges.", (state) => (Operations)Enum.Parse(typeof(Operations), state.Operation, true) == Operations.Shutdown, null)
               .Confirm("You are trying to {Operation} the following virtual machines: {VirtualMachines} Are you sure? Your VMs won't incur charges and all IP addresses will be released.", (state) => (Operations)Enum.Parse(typeof(Operations), state.Operation, true) == Operations.Stop, null)
               .Build();
        }

        public static IForm<RunbookFormState> BuildRunbookForm()
        {
            return CreateCustomForm<RunbookFormState>()
                .Field(new FieldReflector<RunbookFormState>(nameof(RunbookFormState.AutomationAccountName))
                    .SetType(null)
                    .SetPrompt(PerLinePromptAttribute("Please select the automation account you want to use: {||}"))
                    .SetDefine((state, field) =>
                    {
                        foreach (var account in state.AvailableAutomationAccounts)
                        {
                            field
                                .AddDescription(account.AutomationAccountName, account.AutomationAccountName)
                                .AddTerms(account.AutomationAccountName, account.AutomationAccountName);
                        }

                        return Task.FromResult(true);
                    }))
                .Field(new FieldReflector<RunbookFormState>(nameof(RunbookFormState.RunbookName))
                    .SetType(null)
                    .SetPrompt(PerLinePromptAttribute("Please select the runbook you want to run: {||}"))
                    .SetActive(state => !string.IsNullOrWhiteSpace(state.AutomationAccountName))
                    .SetDefine((state, field) =>
                    {
                        if (string.IsNullOrWhiteSpace(state.AutomationAccountName))
                        {
                            return Task.FromResult(false);
                        }

                        foreach (var runbook in state.AvailableAutomationAccounts.Single(
                            a => a.AutomationAccountName == state.AutomationAccountName).Runbooks.Where(x => x.RunbookState.Equals("Published", StringComparison.InvariantCultureIgnoreCase)))
                        {
                            field
                                .AddDescription(runbook.RunbookName, runbook.RunbookName)
                                .AddTerms(runbook.RunbookName, runbook.RunbookName);
                        }

                        return Task.FromResult(true);
                    }))
               .Confirm("Would you like to run runbook '{RunbookName}' of automation acccount '{AutomationAccountName}'?")
               .Build();
        }

        public static IForm<RunbookParameterFormState> BuildRunbookParametersForm()
        {
            return CreateCustomForm<RunbookParameterFormState>()
                .Field(nameof(RunbookParameterFormState.ParameterName), (state) => false)
                .Field(new FieldReflector<RunbookParameterFormState>(nameof(RunbookParameterFormState.ParameterValue))
                    .SetDefine((state, field) =>
                    {
                        var firstParamMessage = state.IsFirstParameter ? $"\n\r If you're unsure what to input, type **quit** followed by **show runbook {state.RunbookName} description** to get more details." : string.Empty;

                        if (!state.IsMandatory)
                        {
                            field.SetOptional(true);

                            field.SetPrompt(new PromptAttribute($"Please enter the value for optional parameter {state.ParameterName} or type *none* to skip it: {firstParamMessage}"));
                        }
                        else
                        {
                            field.SetPrompt(new PromptAttribute($"Please enter the value for mandatory parameter {state.ParameterName}: {firstParamMessage}"));
                        }

                        return Task.FromResult(true);
                    }))
                .Build();
        }

        public static IForm<SecurityTaskFormState> BuildSecurityTaskForm()
        {
            return CreateCustomForm<SecurityTaskFormState>()
                .Field(new FieldReflector<SecurityTaskFormState>(nameof(SecurityTaskFormState.TaskFullName))
                    .SetType(null)
                    .SetPrompt(PerLinePromptAttribute("Please select the security issue you want to fix: {||}"))
                    .SetDefine((state, field) =>
                    {
                        foreach (var task in state.AvailableTasks)
                        {
                            if (task is AzureSqlDatabaseSecurityTask || task is AzureSqlServerSecurityTask)
                                field.AddDescription(task.FullName, task.FullName)
                                    .AddTerms(task.FullName, task.FullName);
                        }

                        return Task.FromResult(true);
                    }))
                .Confirm("Would you like to {TaskFullName}?")
                .Build();
        }

        private static IFormBuilder<T> CreateCustomForm<T>()
           where T : class
        {
            var form = new FormBuilder<T>();
            var command = form.Configuration.Commands[FormCommand.Quit];
            var terms = command.Terms.ToList();
            terms.Add("cancel");
            command.Terms = terms.ToArray();

            var templateAttribute = form.Configuration.Template(TemplateUsage.NotUnderstood);
            var patterns = templateAttribute.Patterns;
            patterns[0] += " Type *cancel* to quit or *help* if you want more information.";
            templateAttribute.Patterns = patterns;

            return form;
        }

        private static PromptAttribute PerLinePromptAttribute(string pattern)
        {
            return new PromptAttribute(pattern)
            {
                ChoiceStyle = ChoiceStyleOptions.PerLine, 
            };
        }
    }
}
