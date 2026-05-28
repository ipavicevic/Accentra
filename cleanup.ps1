# Check staged (pre-installed) packages
Get-AppxPackage -PackageTypeFilter Bundle,Main *Accentra* -AllUsers | Remove-AppxPackage -AllUsers -Confirm

# Check provisioned packages (system-wide)
Get-AppxProvisionedPackage -Online | Where-Object DisplayName -like '*Accentra*' | Remove-AppxProvisionedPackage -Online

# If nothing shows up there either, the leftover may be in the package registry. Check:
Get-Item "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Appx\AppxAllUserStore\*Accentra*" | Remove-Item -Recurse -Force -Confirm
