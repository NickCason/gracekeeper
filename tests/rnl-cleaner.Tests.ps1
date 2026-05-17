BeforeAll {
    $script:ScriptPath = Join-Path (Join-Path (Join-Path $PSScriptRoot "..") "src") "rnl-cleaner.ps1"
    $script:SandboxDir = Join-Path ([System.IO.Path]::GetTempPath()) "rnl-cleaner-tests-$([guid]::NewGuid())"
}

Describe "rnl-cleaner basic deletion" {
    BeforeEach {
        New-Item -ItemType Directory -Force -Path $script:SandboxDir | Out-Null
    }

    AfterEach {
        Remove-Item $script:SandboxDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    It "deletes all .rnl files in the target directory" {
        "data" | Set-Content (Join-Path $script:SandboxDir "a.rnl")
        "data" | Set-Content (Join-Path $script:SandboxDir "b.rnl")
        "keep" | Set-Content (Join-Path $script:SandboxDir "other.txt")

        & $script:ScriptPath -TargetDir $script:SandboxDir -LogFile (Join-Path $script:SandboxDir "test.log")

        (Get-ChildItem $script:SandboxDir -Filter "*.rnl").Count | Should -Be 0
        (Get-ChildItem $script:SandboxDir -Filter "*.txt").Count | Should -Be 1
    }

    It "deletes .rnl files marked Hidden (real FT .rnl files have this attribute)" {
        $hiddenRnl = Join-Path $script:SandboxDir "hidden.rnl"
        "data" | Set-Content $hiddenRnl
        (Get-Item $hiddenRnl).Attributes = 'Hidden'

        & $script:ScriptPath -TargetDir $script:SandboxDir -LogFile (Join-Path $script:SandboxDir "test.log")

        (Get-ChildItem $script:SandboxDir -Filter "*.rnl" -Force).Count | Should -Be 0
    }

    It "no-ops when DISABLED file exists alongside the script" {
        "data" | Set-Content (Join-Path $script:SandboxDir "a.rnl")
        $disabledFile = Join-Path $script:SandboxDir "DISABLED"
        New-Item -ItemType File -Path $disabledFile | Out-Null

        & $script:ScriptPath -TargetDir $script:SandboxDir -LogFile (Join-Path $script:SandboxDir "test.log") -DisabledFile $disabledFile

        (Get-ChildItem $script:SandboxDir -Filter "*.rnl").Count | Should -Be 1
    }

    It "exits 0 when target directory does not exist" {
        $missingDir = Join-Path $script:SandboxDir "does-not-exist"
        $logFile = Join-Path $script:SandboxDir "test.log"

        & $script:ScriptPath -TargetDir $missingDir -LogFile $logFile
        $LASTEXITCODE | Should -Be 0
        Test-Path $logFile | Should -Be $true
        (Get-Content $logFile -Raw) | Should -Match "target dir missing"
    }

    It "writes a summary line with deleted count" {
        "x" | Set-Content (Join-Path $script:SandboxDir "a.rnl")
        "x" | Set-Content (Join-Path $script:SandboxDir "b.rnl")
        $logFile = Join-Path $script:SandboxDir "test.log"

        & $script:ScriptPath -TargetDir $script:SandboxDir -LogFile $logFile

        (Get-Content $logFile -Raw) | Should -Match "deleted=2"
        (Get-Content $logFile -Raw) | Should -Match "locked=0"
        (Get-Content $logFile -Raw) | Should -Match "duration=\d+ms"
    }

    It "records locked files instead of failing" {
        $rnlPath = Join-Path $script:SandboxDir "locked.rnl"
        "x" | Set-Content $rnlPath
        $stream = [System.IO.File]::Open($rnlPath, 'Open', 'Read', 'None')
        try {
            $logFile = Join-Path $script:SandboxDir "test.log"
            & $script:ScriptPath -TargetDir $script:SandboxDir -LogFile $logFile
            $LASTEXITCODE | Should -Be 0
            (Get-Content $logFile -Raw) | Should -Match "locked=1"
            (Get-Content $logFile -Raw) | Should -Match "locked.rnl"
        } finally {
            $stream.Close()
        }
    }

    It "trims log to the last 500 lines" {
        $logFile = Join-Path $script:SandboxDir "test.log"
        1..600 | ForEach-Object { Add-Content $logFile -Value "old line $_" }
        "x" | Set-Content (Join-Path $script:SandboxDir "a.rnl")

        & $script:ScriptPath -TargetDir $script:SandboxDir -LogFile $logFile

        $lines = Get-Content $logFile
        $lines.Count | Should -Be 500
        $lines[-1] | Should -Match "deleted=1"
    }
}
