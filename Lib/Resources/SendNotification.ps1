Param(
    [parameter(Mandatory = $true)]
    [String]
    $Type,

    [ValidateRange(0, [int]::MaxValue)]
    [int]
    $Errors,
    [ValidateRange(0, [int]::MaxValue)]
    [int]
    $Warnings,
    [ValidateRange(0, [double]::MaxValue)]
    [double]
    $Time,

    [ValidateRange(0, [int]::MaxValue)]
    [int]
    $Failed,
    [ValidateRange(0, [int]::MaxValue)]
    [int]
    $Skipped,
    [ValidateRange(0, [int]::MaxValue)]
    [int]
    $Total,
    [ValidateRange(0, [double]::MaxValue)]
    [double]
    $Duration
)

$_ = [Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime]
$_ = [Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType = WindowsRuntime]
$_ = [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime]

$APP_ID = '{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\WindowsPowerShell\v1.0\powershell.exe'

if ($Type -eq "build") {
    if ($Errors -eq 0) {
        $Result = "Build was successful."
        $Icon = "success.png"
    }
    else {
        $Result = "Build failed."
        $Icon = "error.png"
    }

    if ($Time -le 1) {
        $TimeUnit = "ms"
        $Time = $Time * 1000
        $Time = [math]::Round($Time)
    }
    else {
        $TimeUnit = "s"
        $Time = [math]::Round($Time, 1)
    }

    $Line1 = $Result
    $Line2 = "Errors: $($Errors), Warnings: $($Warnings)"
    $Line3 = "Build done in: $($Time)$($TimeUnit)"
}
elseif ($Type -eq "tests") {
    if($Failed -eq 0) {
		$Result = "Tests finished."
        $Icon = "success.png"
    }
    else {
        $Result = "Tests failed."
        $Icon = "error.png"
    }

    if ($Duration -le 1) {
        $DurationUnit = "ms"
        $Duration = $Duration * 1000
        $Duration = [math]::Round($Duration)
    }
    else {
        $DurationUnit = "s"
        $Duration = [math]::Round($Duration, 1)
    }

    $Line1 = $Result
    $Line2 = "Errors: $($Failed), Skipped: $($Skipped), Total: $($Total)"
    $Line3 = "Duration: $($Duration)$($DurationUnit)"
}

$Template = @"
<toast duration="short">
    <audio silent="true"/>
    <visual>
        <binding template="ToastImageAndText04" baseUri="file:///$($PSScriptRoot)/">
            <image id="1" src="$($Icon)" />
            <text id="1">$($Line1)</text>
            <text id="2">$($Line2)</text>
            <text id="3">$($Line3)</text>
        </binding>
    </visual>
</toast>
"@

$Xml = New-Object Windows.Data.Xml.Dom.XmlDocument
$Xml.LoadXml($Template)
$ToastNotification = [Windows.UI.Notifications.ToastNotification]::new($Xml)

[Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier($APP_ID).Show($ToastNotification)