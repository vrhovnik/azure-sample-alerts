# Working with alerts in Azure

# Enable monitoring

In order to use the alerting you need to enable the machines to be onboarded to monitoring.

![how alerts work](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/media/alerts-overview/alerts.png)

You should specify alert [processing rules](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-processing-rules?tabs=portal#suppress-notifications-during-planned-maintenance):

1. you might have many alerts rules that covers the resource - updating all of them can be time and performance consuming
2. maybe only few resources go through maintenance and you need others to be under alert control

To use it via command line, you need to install module - for example PowerShell:

``
Install-Module Az.AlertsManagement
``

The alerts are on the managed app level and are deployed within the managed resource group:

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

Sending heart beats every min to get info (if VM is up or down)

``
Heartbeat
| summarize TimeGenerated=max(TimeGenerated) by Computer, _ResourceId
| extend Duration = datetime_diff('minute',now(),TimeGenerated)
| summarize AggregatedValue = min(Duration) by Computer, bin(TimeGenerated,5m), _ResourceId
``

Checking power option via PowerShell:

``
Get-AzVM -ResourceGroupName "MyRG" -Name "MyVM" -Status | Select-Object Name, ResourceGroupName, PowerState, ProvisioningState
``

Preview option ([recommended alerts](https://learn.microsoft.com/en-us/azure/azure-monitor/vm/tutorial-monitor-vm-alert-recommended))

If you are using Azure Functions, you an also take an advantage of [Start/Stop v2 integration](https://learn.microsoft.com/en-us/azure/azure-functions/start-stop-vms/overview).

**Links:**

* [Create availability alert rule for multiple Azure virtual machines (preview)](https://learn.microsoft.com/en-us/azure/azure-monitor/vm/tutorial-monitor-vm-alert-availability)
* [VM Heartbeat](https://learn.microsoft.com/en-us/azure/azure-monitor/vm/monitor-virtual-machine-alerts#log-alert-rules-1) - [heartbeat table definition](https://learn.microsoft.com/en-us/azure/azure-monitor/reference/tables/heartbeat)


## VMSS doesn't have the minimum number of servers

Can be achieved with [Alertv2](https://learn.microsoft.com/en-us/powershell/module/az.monitor/add-azmetricalertrulev2?view=azps-10.2.0). 

```powershell

$vmss = Get-AzVmss -ResourceGroupName "MyRG" -Name "MyScaleSet" -Status

$criteria = New-AzMetricAlertRuleV2Criteria -MetricName "InstanceCount" -Operator LessThan -Threshold 2 -TimeAggregation Average

New-AzMetricAlertRuleV2 -Name "InstanceCountAlert" -Description "Alert when instance count is less than 2" 
-ResourceGroupName "MyRG" -ActionGroup "/subscriptions/your-subscription-id/resourceGroups/MyRG/providers/microsoft.insights/actionGroups/MyActionGroup" 
-TargetResourceId $vmss.Id -Condition $criteria -Severity 3 -Frequency 5 -WindowSize 15

Get-AzMetricAlertRuleV2 -ResourceGroupName "MyRG" -Name "InstanceCountAlert"

```


``
AzureMetrics
| where ResourceId == "/subscriptions/your-subscription-id/resourceGroups/MyRG/providers/Microsoft.Compute/virtualMachineScaleSets/MyScaleSet"
| where TimeGenerated > ago(24h)
| where MetricName == "InstanceCount"
| summarize avg(InstanceCount) by bin(TimeGenerated, 1h)
``

## OS disk full

by using log query

``
Perf
| where ResourceId == "/subscriptions/your-subscription-id/resourceGroups/MyRG/providers/Microsoft.Compute/virtualMachines/MyVM"
| where TimeGenerated > ago(24h)
| where ObjectName == "LogicalDisk" and CounterName == "Free Megabytes"
| project TimeGenerated, InstanceName, FreeSpaceMB, SizeMB = CounterValue + FreeSpaceMB
``

## Planned maintenance alerts

In the Azure portal, navigate to the Service Health page under the Monitor section. Click on Planned Maintenance and then click on New alert rule.

With PowerShell:


```powershell
# Get the maintenance status of the VM
$vmStatus = Get-AzVM -ResourceGroupName myResourceGroup -Name myVM -Status

# Check if there is any maintenance planned for the VM
if ($vmStatus.MaintenanceRedeployStatus) {

# Get the start time of the maintenance window
$maintenanceStartTime = $vmStatus.MaintenanceRedeployStatus.MaintenanceWindowStartTime

# Calculate the time difference between the current time and the maintenance start time
$timeDiff = New-TimeSpan -Start (Get-Date) -End $maintenanceStartTime

# Check if the maintenance is scheduled within the next 24 hours
if ($timeDiff.TotalHours -le 24) {

# Create an alert rule that sends an email notification to a specified email address
New-AzAlertRule -Name "Maintenance Alert" -ResourceGroupName myResourceGroup -TargetResourceId $vmStatus.Id -Condition @{"odata.type"="Microsoft.Azure.Management.Insights.Models.ThresholdRuleCondition"; "DataSource"=@{"odata.type"="Microsoft.Azure.Management.Insights.Models.RuleMetricDataSource"; "ResourceId"=$vmStatus.Id; "MetricName"="MaintenanceRedeployStatus"}; "Operator"="GreaterThan"; "Threshold"=0} -Action @{"odata.type"="Microsoft.Azure.Management.Insights.Models.RuleEmailAction"; "SendToServiceOwners"=$false; "CustomEmails"=@("example@example.com")}
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

Check AlertCall solution.

# Common links

* [Types of alert](https://learn.microsoft.com/en-us/azure/azure-monitor/alerts/alerts-overview#types-of-alerts)
* [Alerts change state](https://learn.microsoft.com/en-us/rest/api/monitor/alertsmanagement/alerts/change-state?tabs=HTTP)
