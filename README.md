# Working with alerts in Azure

![how alerts work](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/media/alerts-overview/alerts.png)

To create an alert rule, you must have:

1. Read permission on the target resource of the alert rule.
2. Write permission on the resource group in which the alert rule is created. If you're creating the alert rule from the
   Azure portal, the alert rule is created by default in the same resource group in which the target resource resides.
3. Read permission on any action group associated with the alert rule, if applicable.

## Processing rules

You should specify
alert [processing rules](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-processing-rules?tabs=portal#suppress-notifications-during-planned-maintenance):

1. you might have many alerts rules that covers the resource - updating all of them can be time and performance
   consuming
2. maybe only few resources go through maintenance and you need others to be under alert control

To use it via command line on subscription level, you need to install module (and configure connection to the
subscription) - for example PowerShell:

``
Connect-AzAccount -SubscriptionId XXXX-xxxxx-xxxxxx-xxxxx
Install-Module Az.AlertsManagement
``

Requested scenarios:

1. Basic VM up or down (deallocated or stopped)
2. VMSS doesn't have the minimum number of servers
3. OS disk full
4. Planned maintenance alerts
5. Resource in the managed resource group deleted
6. We will have a App Function that will pull internal application events and based on this we need to trigger alerts
7. Action groups and webhooks

## Basic MV up or down

Deallocate:

``
az monitor activity-log alert create
--name cc-deallocate-virtual-machine-alert
--description "Alert triggered by Deallocate Virtual Machine events"
--resource-group rg-lb-alerts
--action-group "/subscriptions/your-subscription-id/resourcegroups/rg-lb-alerts/providers/microsoft.insights/actiongroups/yourtestgroup"
--condition category=Administrative and operationName=Microsoft.Compute/virtualMachines/deallocate/action
``

Sending heartbeats every min to get info (if VM is up or down)

``
Heartbeat
| summarize TimeGenerated=max(TimeGenerated) by Computer, _ResourceId
| extend Duration = datetime_diff('minute',now(),TimeGenerated)
| summarize AggregatedValue = min(Duration) by Computer, bin(TimeGenerated,5m), _ResourceId
``

Or VM availability with chart

``
// Track VM Availability using Heartbeat  
// Display the VM's reported availability during the last hour.
InsightsMetrics
| where TimeGenerated > ago(1h)
| where Origin == "vm.azm.ms"
| where Namespace == "Computer"
| where Name == "Heartbeat"
| summarize heartbeat_count = count() by bin(TimeGenerated, 5m), Computer
| extend alive=iff(heartbeat_count > 2, 1.0, 0.0) //computer considered "down" if it has 2 or fewer heartbeats in 5 min interval
| project TimeGenerated, alive, Computer
| render timechart with (ymin = 0, ymax = 1)
``

Checking power option via PowerShell:

``
Get-AzVM -ResourceGroupName "MyRG" -Name "MyVM" -Status | Select-Object Name, ResourceGroupName, PowerState, ProvisioningState
``

Preview
option ([recommended alerts](https://learn.microsoft.com/en-us/azure/azure-monitor/vm/tutorial-monitor-vm-alert-recommended))

![Deallocate](https://webeudatastorage.blob.core.windows.net/web/machine-signal-for-deallocate.png)

If you are using Azure Functions, you an also take an advantage
of [Start/Stop v2 integration](https://learn.microsoft.com/en-us/azure/azure-functions/start-stop-vms/overview).

**Links:**

* [Create availability alert rule for multiple Azure virtual machines (preview)](https://learn.microsoft.com/en-us/azure/azure-monitor/vm/tutorial-monitor-vm-alert-availability)
* [VM Heartbeat](https://learn.microsoft.com/en-us/azure/azure-monitor/vm/monitor-virtual-machine-alerts#log-alert-rules-1) - [heartbeat table definition](https://learn.microsoft.com/en-us/azure/azure-monitor/reference/tables/heartbeat)

## VMSS doesn't have the minimum number of servers

Can be achieved
with [Alertv2](https://learn.microsoft.com/en-us/powershell/module/az.monitor/add-azmetricalertrulev2?view=azps-10.2.0).

```powershell

$vmss = Get-AzVmss -ResourceGroupName "MyRG" -Name "MyScaleSet" -Status

$criteria = New-AzMetricAlertRuleV2Criteria -MetricName "InstanceCount" -Operator LessThan -Threshold 2 -TimeAggregation Average

New-AzMetricAlertRuleV2 -Name "InstanceCountAlert" -Description "Alert when instance count is less than 2"
-ResourceGroupName "MyRG" -ActionGroup "/subscriptions/your-subscription-id/resourceGroups/MyRG/providers/microsoft.insights/actionGroups/MyActionGroup"
-TargetResourceId $vmss.Id -Condition $criteria -Severity 3 -Frequency 5 -WindowSize 15

Get-AzMetricAlertRuleV2 -ResourceGroupName "MyRG" -Name "InstanceCountAlert"

```

or by using Azure metrics

``
InsightsMetrics
| where Origin == "vm.azm.ms"
| distinct Computer, Origin
| summarize Computer = count() by Origin
``

## OS disk full

By using log query

``
// Virtual Machine free disk space  
// Show the latest report of free disk space, per instance.
InsightsMetrics
| where TimeGenerated > ago(1h)
| where Origin == "vm.azm.ms"
| where Namespace == "LogicalDisk"
| where Name == "FreeSpaceMB"
| extend t=parse_json(Tags)
| summarize arg_max(TimeGenerated, *) by tostring(t["vm.azm.ms/mountId"]), Computer // arg_max over TimeGenerated returns the latest record
| project Computer, TimeGenerated, t["vm.azm.ms/mountId"], Val
``

![machine space](https://webeudatastorage.blob.core.windows.net/web/machine-space.png)

## Planned maintenance alerts

In the Azure portal, navigate to the Service Health page under the Monitor section. Click on Planned Maintenance and
then click on New alert rule.

With PowerShell:

```powershell
# Get the maintenance status of the VM
$vmStatus = Get-AzVM -ResourceGroupName myResourceGroup -Name myVM -Status

# Check if there is any maintenance planned for the VM
if ($vmStatus.MaintenanceRedeployStatus)
{

    # Get the start time of the maintenance window
    $maintenanceStartTime = $vmStatus.MaintenanceRedeployStatus.MaintenanceWindowStartTime

    # Calculate the time difference between the current time and the maintenance start time
    $timeDiff = New-TimeSpan -Start (Get-Date) -End $maintenanceStartTime

    # Check if the maintenance is scheduled within the next 24 hours
    if ($timeDiff.TotalHours -le 24)
    {

        # Create an alert rule that sends an email notification to a specified email address
        New-AzAlertRule -Name "Maintenance Alert" -ResourceGroupName myResourceGroup -TargetResourceId $vmStatus.Id -Condition @{ "odata.type" = "Microsoft.Azure.Management.Insights.Models.ThresholdRuleCondition"; "DataSource" = @{ "odata.type" = "Microsoft.Azure.Management.Insights.Models.RuleMetricDataSource"; "ResourceId" = $vmStatus.Id; "MetricName" = "MaintenanceRedeployStatus" }; "Operator" = "GreaterThan"; "Threshold" = 0 } -Action @{ "odata.type" = "Microsoft.Azure.Management.Insights.Models.RuleEmailAction"; "SendToServiceOwners" = $false; "CustomEmails" = @("example@example.com") }
    }
}

```

## Resource in the managed resource group deleted

``
AzureActivity
| where OperationNameValue endswith "DELETE"
``

Can be also on resource level for specific resources from activity log.

## Trigger events programmatically

We will have a App Function that will pull internal application events and based on this we need to trigger alerts.

Check [AlertCall](AlertCall) solution.

# Common links

* [Monitoring contributor](https://learn.microsoft.com/en-us/azure/azure-monitor/roles-permissions-security#monitoring-contributor)
* [Create custom role](https://learn.microsoft.com/en-us/azure/role-based-access-control/custom-roles)
* [Types of alert](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-overview#types-of-alerts)
* [Alerts change state](https://learn.microsoft.com/en-us/rest/api/monitor/alertsmanagement/alerts/change-state?tabs=HTTP)
* [Azure Advertizer](https://www.azadvertizer.net/)
* [Best practices for monitoring virtual machines in Azure Monitor](https://learn.microsoft.com/en-us/azure/azure-monitor/best-practices-vm)
* [Common alert schema](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-common-schema#sample-alert-payload)
