<#
.SYNOPSIS
  Pulls the newest database backup from the server onto this computer and a flash drive.

.DESCRIPTION
  Runs on the gym's Windows computer (Task Scheduler, see README "Backup and restore").
  It PULLS: this computer sits behind the gym's router, so the server cannot reach it, and the
  key it uses belongs to a read-only account that can do nothing but read the backups.

  Exit codes, which Task Scheduler shows as "Last Run Result":
    0  the newest backup is here and on the flash drive (or no flash drive was asked for)
    1  it failed, or the server's newest backup is too old (the nightly job has stopped)
    2  the backup is on this computer, but the flash drive was not attached

.EXAMPLE
  .\pull-backup.ps1 -RemoteHost gymbackup@gym.example.ir -KeyFile C:\GymBackup\id_ed25519 `
      -LocalDir C:\GymBackups -FlashDir E:\GymBackups
#>
[CmdletBinding()]
param(
  # user@host of the read-only backup account on the server.
  [Parameter(Mandatory)] [string] $RemoteHost,
  [Parameter(Mandatory)] [string] $KeyFile,
  [string] $RemoteDir = '/opt/gym/backups',
  [string] $LocalDir = 'C:\GymBackups',
  # Optional second copy on an attached flash drive, for example E:\GymBackups.
  [string] $FlashDir,
  [int] $KeepLocal = 30,
  # The nightly job runs once a day; a newest backup older than this means it has stopped.
  [int] $MaxAgeHours = 36
)

$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $LocalDir | Out-Null
$logFile = Join-Path $LocalDir 'pull-backup.log'

function Write-Log([string] $level, [string] $message) {
  $line = '{0} [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $level, $message
  Add-Content -Path $logFile -Value $line -Encoding UTF8
  Write-Host $line
}

# BatchMode: never wait for a password or a question at 4 a.m.; fail instead.
# IdentitiesOnly: use this key and nothing else that happens to be loaded in an agent.
# StrictHostKeyChecking=yes: connect only to a server whose key was accepted once by hand.
$sshOptions = @('-i', $KeyFile, '-o', 'BatchMode=yes', '-o', 'IdentitiesOnly=yes', '-o', 'StrictHostKeyChecking=yes')

function Remove-OldBackups([string] $dir, [int] $keep) {
  # File names carry the timestamp, so a name sort is a date sort.
  Get-ChildItem -Path $dir -Filter 'gym-*.dump' -File |
    Sort-Object Name -Descending |
    Select-Object -Skip $keep |
    ForEach-Object {
      Remove-Item -LiteralPath $_.FullName -Force
      Write-Log 'INFO' "removed old backup $($_.FullName)"
    }
}

function Copy-Verified([string] $source, [string] $destinationDir, [long] $expectedSize) {
  New-Item -ItemType Directory -Force -Path $destinationDir | Out-Null
  $name = Split-Path -Leaf $source
  $final = Join-Path $destinationDir $name
  $partial = Join-Path $destinationDir ".$name.partial"
  Copy-Item -LiteralPath $source -Destination $partial -Force
  if ((Get-Item -LiteralPath $partial).Length -ne $expectedSize) {
    Remove-Item -LiteralPath $partial -Force
    throw "copy to $destinationDir has the wrong size"
  }
  Move-Item -LiteralPath $partial -Destination $final -Force
}

try {
  # One call: modification time (epoch seconds), size in bytes and path of the newest dump.
  $remoteCommand = "stat -c '%Y %s %n' `$(ls -1t $RemoteDir/gym-*.dump | head -n 1)"
  $info = & ssh @sshOptions $RemoteHost $remoteCommand
  if ($LASTEXITCODE -ne 0 -or -not $info) {
    throw "could not list backups on $RemoteHost (ssh exit code $LASTEXITCODE)"
  }

  $parts = ($info | Select-Object -First 1).Trim() -split ' ', 3
  $remoteEpoch = [long] $parts[0]
  $remoteSize = [long] $parts[1]
  $remotePath = $parts[2]
  $name = $remotePath.Substring($remotePath.LastIndexOf('/') + 1)

  $ageHours = ([DateTimeOffset]::UtcNow.ToUnixTimeSeconds() - $remoteEpoch) / 3600
  $stale = $ageHours -gt $MaxAgeHours

  $localFile = Join-Path $LocalDir $name
  if ((Test-Path -LiteralPath $localFile) -and ((Get-Item -LiteralPath $localFile).Length -eq $remoteSize)) {
    Write-Log 'INFO' "already have $name"
  } else {
    $partial = Join-Path $LocalDir ".$name.partial"
    Write-Log 'INFO' "downloading $name ($remoteSize bytes)"
    & scp @sshOptions "${RemoteHost}:${remotePath}" $partial
    if ($LASTEXITCODE -ne 0) { throw "scp failed (exit code $LASTEXITCODE)" }

    # A backup that arrived short is worse than none: it looks like one.
    if ((Get-Item -LiteralPath $partial).Length -ne $remoteSize) {
      Remove-Item -LiteralPath $partial -Force
      throw "$name arrived with the wrong size"
    }
    Move-Item -LiteralPath $partial -Destination $localFile -Force
    Write-Log 'INFO' "saved $localFile"
  }
  Remove-OldBackups $LocalDir $KeepLocal

  $flashMissing = $false
  if ($FlashDir) {
    $root = Split-Path -Qualifier $FlashDir
    if ($root -and (Test-Path -LiteralPath "$root\")) {
      Copy-Verified $localFile $FlashDir $remoteSize
      Remove-OldBackups $FlashDir $KeepLocal
      Write-Log 'INFO' "copied to $FlashDir"
    } else {
      $flashMissing = $true
      Write-Log 'WARN' "flash drive $root is not attached; only this computer has the backup"
    }
  }

  if ($stale) {
    Write-Log 'ERROR' ("the newest backup on the server is {0:N0} hours old; the nightly job on the server has stopped" -f $ageHours)
    exit 1
  }
  if ($flashMissing) { exit 2 }
  Write-Log 'INFO' 'done'
  exit 0
}
catch {
  Write-Log 'ERROR' $_.Exception.Message
  exit 1
}
