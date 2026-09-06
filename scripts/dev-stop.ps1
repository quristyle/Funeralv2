# ============================================================
# 서비스 하나를 확실히 내린다 (dev.bat 의 stop_service 가 부른다)
# ============================================================
#
# 사용법
#   powershell -NoProfile -ExecutionPolicy Bypass -File dev-stop.ps1 -Port 5350 -Dir C:\Funeralv2\microservices\FileServer
#
# 프론트처럼 디렉터리를 공유하는 서비스는 기동 명령을 함께 넘긴다.
#   ... -Port 5556 -Dir C:\Funeralv2\fronts -CmdMatch "pnpm --filter @jsini/site dev"
#
# 출력은 **한 줄**이다. dev.bat 이 첫 낱말만 읽는다.
#   NOT_RUNNING            떠 있지 않았다
#   STOPPED                내렸다 (포트가 비었음을 확인했다)
#   FAILED pid=1234,5678   내리지 못했다. 남은 PID 를 함께 적는다
#
# ── 왜 배치가 아니라 PowerShell 인가 ─────────────────────────
#
# 1) 배치에는 부모 프로세스를 따라 올라갈 방법이 없다.
#    이 저장소의 서비스는 셸 → 런처 → 실제 프로세스로 겹쳐 뜨는데,
#    포트를 잡고 있는 것은 맨 아래 자식이다.
#      백엔드   cmd /k → dotnet run → <서비스>.exe
#      프론트   cmd /k pnpm dev → node → cmd → node → cmd → node → cmd → node(vite) → dart
#    자식만 죽이면 위의 셸들이 그대로 남는다. 실제로 프론트는 여덟 겹이다.
#
# 2) taskkill 의 종료 코드를 믿을 수 없다.
#    · 창 제목 필터(/FI "WINDOWTITLE eq ...")는 **하나도 맞지 않아도 0** 을 돌려준다.
#      ("INFO: No tasks running with the specified criteria." 를 찍고 성공으로 끝난다)
#      그래서 안 떠 있는 서비스도 "종료했다"고 보고하게 된다.
#    · /T 는 액세스 거부(5) 를 간헐적으로 낸다. 이때 프로세스는 살아남는다.
#    그래서 여기서는 종료 코드를 보지 않고 **포트가 비었는지**로 판정한다.
#
#    (윈도우 콘솔 창 제목은 애초에 기댈 수 없다. `start "이름"` 으로 띄운 창도
#     tasklist /v 에서 Window Title 이 N/A 로 나오는 경우가 있다.)
#
# 리눅스/맥 스크립트가 작업 디렉터리(cwd)로 프로세스를 고르는 것과 같은 자리다.
# 윈도우에는 /proc 이 없어 cwd 를 읽을 수 없으므로, 여기서는
# **포트 + 실행 파일 경로 + 부모 사슬** 셋으로 같은 일을 한다.
# ============================================================

[CmdletBinding()]
param(
    # 이 서비스가 잡고 있어야 하는 포트
    [Parameter(Mandatory = $true)][int]    $Port,

    # 이 서비스의 디렉터리. 이 아래에서 실행된 프로세스는 이 서비스의 것으로 본다.
    [Parameter(Mandatory = $true)][string] $Dir,

    # 이것이 주어지면 **디렉터리로 후보를 모으지 않고** 명령줄에 이 문구가 든 것만 본다.
    #
    # 프론트엔드 때문에 생겼다. 프론트가 둘(업무 포털 5555 · 소개 사이트 5556)인데
    # 둘 다 `fronts` 아래에서 돌고 node 실행 파일도 `fronts\node_modules` 하나를 공유한다.
    # 그래서 디렉터리로 고르면 **한쪽을 내릴 때 다른 쪽까지 죽는다** (실제로 겪었다).
    #
    # 여기에는 dev.bat 의 기동 명령을 그대로 넘긴다
    # (예: `pnpm --filter @jsini/site dev`). 정규식이 아니라 **문자열 그대로** 찾는다.
    [string] $CmdMatch = '',

    # 포트가 비기를 기다리는 시간
    [int] $TimeoutSec = 10
)

$ErrorActionPreference = 'Stop'

$dirFull = try { [IO.Path]::GetFullPath($Dir).TrimEnd('\') } catch { $Dir.TrimEnd('\') }

# ------------------------------------------------------------
# 프로세스 한 장면(snapshot). 부모·자식 관계를 여기서 다 읽는다.
# WMI 조회는 느리므로 한 번만 부르고 표로 만들어 쓴다.
# ------------------------------------------------------------
function Read-Snapshot {
    $byPid    = @{}
    $children = @{}

    foreach ($p in Get-CimInstance Win32_Process -Property ProcessId, ParentProcessId, Name, CommandLine, ExecutablePath) {
        $procId   = [int]$p.ProcessId
        $parentId = [int]$p.ParentProcessId

        $byPid[$procId] = $p
        if (-not $children.ContainsKey($parentId)) { $children[$parentId] = New-Object 'System.Collections.Generic.List[int]' }
        $children[$parentId].Add($procId)
    }

    [pscustomobject]@{ ByPid = $byPid; Children = $children }
}

# 지금 그 포트를 LISTENING 중인 PID. 스크립트 밖에서 띄운 서버도 이걸로 잡힌다.
function Get-PortPid([int]$p) {
    try {
        @(Get-NetTCPConnection -State Listen -LocalPort $p -ErrorAction Stop |
            Select-Object -ExpandProperty OwningProcess -Unique) |
            Where-Object { $_ -gt 4 } | ForEach-Object { [int]$_ }
    }
    catch { @() }   # 그 포트에 아무것도 없으면 Get-NetTCPConnection 이 예외를 낸다
}

# ------------------------------------------------------------
# 이 서비스에 속한 프로세스로 볼 수 있는 것만 고른다.
# 그 디렉터리에서 열어 둔 편집기나 셸까지 죽이면 안 된다.
# ------------------------------------------------------------
# 프론트엔드는 한 겹이 아니다. `cmd /k pnpm dev` 아래로
#   node(corepack) → cmd /d /s /c pnpm -F @vben/jsini-portal run dev → node → cmd → node → cmd → node(vite)
# 이렇게 여덟 겹이 겹쳐 뜨므로, 중간의 pnpm·corepack 호출도 전부 알아봐야 한다.
# 하나라도 못 알아보면 거기서 거슬러 올라가기가 멈추고 위쪽 셸이 남는다.
#
# 넉넉해 보이지만 위험하지 않다. 이 판정은 **이미 이 서비스의 것으로 확인된
# 프로세스의 직계 조상**에만 쓰이고, dev.bat 자신과 그 조상은 따로 보호한다.
#
# ── `dotnet-watch` 를 반드시 넣는다 (실제로 밟았다) ──────────
#
# 감시 모드의 사슬은 네 겹이다.
#
#   cmd /k dotnet watch run                     ← 여기까지 내려야 한다
#     └ dotnet watch run
#         └ dotnet "…\DotnetTools\dotnet-watch\…\dotnet-watch.dll" …
#             └ dotnet run --no-build --framework …
#                 └ <서비스>.exe                ← 포트를 잡고 있는 것
#
# 세 번째 줄의 명령줄에는 `dotnet-watch.dll` 이 있을 뿐 「dotnet 공백 watch」가
# 없다. 그래서 `dotnet\s+(run|watch)` 로는 안 걸리고, 거기서 거슬러 올라가기가
# 멈춰 **위의 두 겹이 살아남았다.**
#
# 살아남은 `dotnet watch` 는 자식이 죽은 것을 보고 **다시 띄운다.** 그래서
#   · dev.bat 이 "종료" 라고 찍은 뒤 잠시 뒤 서비스가 되살아나고
#   · 되살아난 놈이 DLL 을 물어 다른 창의 빌드가 MSB3027 로 실패하고
#   · 재기동할 때마다 창이 하나씩 쌓인다 (실제로 58개가 쌓여 있었다)
#
$devPattern = 'dotnet\s+(run|watch)|dotnet-watch|corepack|pnpm|vite'

function Test-DevProcess($proc) {
    if (-not $proc) { return $false }

    # 이 서비스 디렉터리 안에서 실행된 것 (<서비스>.exe, node_modules 안의 도구 등)
    $exe = $proc.ExecutablePath
    if ($exe -and $exe.StartsWith($dirFull, [StringComparison]::OrdinalIgnoreCase)) { return $true }

    # 개발 서버를 띄우는 명령줄
    $cmd = $proc.CommandLine
    if ($cmd -and $cmd -match $devPattern) { return $true }

    return $false
}

# ------------------------------------------------------------
# 씨앗 PID 에서 부모를 따라 올라가, 이 서비스를 띄운 **맨 위 프로세스**를 찾는다.
# 여기가 보통 dev.bat 이 start 로 띄운 `cmd /k ...` 창이다.
#
# 멈추는 조건 — 하나라도 걸리면 더 올라가지 않는다.
#   · 부모가 이 스크립트의 조상이다   (dev.bat 자신을 죽이면 안 된다)
#   · 부모가 이미 사라졌다            (앞선 dev.bat 실행이 남긴 것)
#   · 부모가 개발 서버로 안 보인다    (탐색기, 터미널 등)
# ------------------------------------------------------------
function Get-ChainRoot([int]$seed, $snap, $protectedPids) {
    $top = $seed
    $cur = $seed

    for ($depth = 0; $depth -lt 16; $depth++) {
        if (-not $snap.ByPid.ContainsKey($cur)) { break }

        $parent = [int]$snap.ByPid[$cur].ParentProcessId
        if ($parent -le 4)                        { break }
        if (-not $snap.ByPid.ContainsKey($parent)) { break }
        if ($protectedPids.Contains($parent))      { break }
        if (-not (Test-DevProcess $snap.ByPid[$parent])) { break }

        $top = $parent
        $cur = $parent
    }

    $top
}

# 맨 위 프로세스 아래에 딸린 것 전부. 부모를 죽여도 윈도우는 자식을 안 내리므로 다 모아야 한다.
function Get-Subtree([int]$root, $snap) {
    $found = New-Object 'System.Collections.Generic.List[int]'
    $seen  = New-Object 'System.Collections.Generic.HashSet[int]'
    $queue = New-Object 'System.Collections.Generic.Queue[int]'
    $queue.Enqueue($root)

    while ($queue.Count -gt 0) {
        $p = $queue.Dequeue()
        if (-not $seen.Add($p)) { continue }
        $found.Add($p)

        if ($snap.Children.ContainsKey($p)) {
            foreach ($kid in $snap.Children[$p]) { $queue.Enqueue($kid) }
        }
    }

    $found
}

# ------------------------------------------------------------
# 이 서비스에 속한 프로세스를 모은다.
#   · 포트를 잡고 있는 것
#   · 포트를 못 잡은 채 떠 있는 것 (기동 실패 등) — 이것을 어떻게 찾을지가 갈린다
#       -CmdMatch 가 있으면  명령줄에 그 문구가 든 것
#       없으면               이 디렉터리 안에서 실행된 것
# 각각에서 위로 올라가 맨 위를 찾고, 그 아래 전체를 대상으로 삼는다.
# ------------------------------------------------------------
function Get-ServicePids($snap, $protectedPids) {
    $seeds = New-Object 'System.Collections.Generic.HashSet[int]'

    foreach ($procId in Get-PortPid $Port) { [void]$seeds.Add($procId) }

    if ($CmdMatch) {
        # 프론트처럼 디렉터리를 공유하는 경우. 명령줄로만 고른다.
        foreach ($proc in $snap.ByPid.Values) {
            $cmd = $proc.CommandLine
            if ($cmd -and $cmd.IndexOf($CmdMatch, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                [void]$seeds.Add([int]$proc.ProcessId)
            }
        }
    }
    else {
        foreach ($proc in $snap.ByPid.Values) {
            $exe = $proc.ExecutablePath
            if ($exe -and $exe.StartsWith($dirFull, [StringComparison]::OrdinalIgnoreCase)) {
                [void]$seeds.Add([int]$proc.ProcessId)
            }
        }
    }

    $targets = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($seed in $seeds) {
        if ($protectedPids.Contains($seed)) { continue }

        $root = Get-ChainRoot $seed $snap $protectedPids
        foreach ($procId in Get-Subtree $root $snap) {
            if (-not $protectedPids.Contains($procId)) { [void]$targets.Add($procId) }
        }
    }

    $targets
}

# 이 스크립트 자신과 조상 프로세스는 절대 죽이지 않는다.
function Get-ProtectedPids($snap) {
    $protectedPids = New-Object 'System.Collections.Generic.HashSet[int]'
    $cur = $PID

    for ($depth = 0; $depth -lt 32; $depth++) {
        if (-not $snap.ByPid.ContainsKey($cur)) { break }
        [void]$protectedPids.Add($cur)
        $cur = [int]$snap.ByPid[$cur].ParentProcessId
        if ($cur -le 4) { break }
    }

    $protectedPids
}

# ------------------------------------------------------------
# 실제로 내린다
# ------------------------------------------------------------
$snap          = Read-Snapshot
$protectedPids = Get-ProtectedPids $snap
$targets       = Get-ServicePids $snap $protectedPids

if ($targets.Count -eq 0) {
    # 죽일 것이 없다. 단, 포트를 잡고 있는 것이 있으면 "안 떠 있다"고 해선 안 된다.
    # 이 스크립트의 조상이거나 우리가 이 서비스의 것으로 볼 수 없는 프로세스가
    # 그 포트를 쓰고 있는 경우다. status 와 어긋나는 보고를 남기지 않는다.
    $holder = @(Get-PortPid $Port)
    if ($holder.Count -eq 0) {
        Write-Output 'NOT_RUNNING'
        exit 0
    }

    Write-Output ('FAILED pid={0}' -f ($holder -join ','))
    exit 1
}

# 위(맨 위 셸)부터 보낸다. **감시 런처가 자식을 다시 띄우지 못하게** 하려는 것이다.
# 기동 명령이 `dotnet watch run` 이라 이 순서가 실제로 중요하다 — 자식을 먼저
# 죽이면 그 사이에 감시가 새로 띄운다.
foreach ($pass in 1, 2) {
    foreach ($procId in ($targets | Sort-Object)) {
        try { Stop-Process -Id $procId -Force -ErrorAction Stop } catch { }
    }

    # 포트가 비기를 기다린다. taskkill/Stop-Process 의 성공 여부가 아니라 이것으로 판정한다.
    $deadline = (Get-Date).AddSeconds($TimeoutSec / 2)
    while ((Get-Date) -lt $deadline) {
        if (-not (Get-PortPid $Port)) { break }
        Start-Sleep -Milliseconds 250
    }

    # 한 번에 안 죽는 경우가 있어(액세스 거부 등) 남은 것을 다시 훑는다.
    $snap    = Read-Snapshot
    $targets = Get-ServicePids $snap (Get-ProtectedPids $snap)
    if ($targets.Count -eq 0 -and -not (Get-PortPid $Port)) { break }
}

$leftover = @(Get-PortPid $Port) + @($targets) | Sort-Object -Unique

if ($leftover.Count -eq 0) {
    Write-Output 'STOPPED'
    exit 0
}

Write-Output ('FAILED pid={0}' -f ($leftover -join ','))
exit 1
