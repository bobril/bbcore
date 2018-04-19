Param(
	[parameter(Mandatory=$true)]
	[ValidateRange(0,[int]::MaxValue)]
	[int]
	$Errors,

	[parameter(Mandatory=$true)]
	[ValidateRange(0,[int]::MaxValue)]
	[int]
	$Warnings,

	[parameter(Mandatory=$true)]
	[ValidateRange(0,[int]::MaxValue)]
	[double]
	$Time
)

$_ = [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]
$_ = [Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime]
$_ = [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]

$APP_ID = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'

if($Errors -eq 0)
{
	$Result = "Build was successful."
	$Icon = "success.png"
}
else
{
	$Result = "Build failed."
	$Icon = "error.png"
}

if ($Time -le 1)
{
	$TimeUnit = "ms"
	$Time = $Time * 1000
}
else
{
	$TimeUnit = "s"
}

$Template = @"
<toast duration="short">
	<visual>
		<binding template="ToastImageAndText04" baseUri="file:///$($PSScriptRoot)/">
			<image id="1" src="$($Icon)" />
			<text id="1">$($Result)</text>
			<text id="2">Errors: $($Errors), Warnings: $($Warnings)</text>
			<text id="3">Build done in: $($Time)$($TimeUnit)</text>
		</binding>
	</visual>
</toast>
"@

$Xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$Xml.LoadXml($Template)
$ToastNotification = [Windows.UI.Notifications.ToastNotification]::new($Xml)

[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($APP_ID).Show($ToastNotification)