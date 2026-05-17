#Requires AutoHotkey v2.0
#SingleInstance Force
#NoTrayIcon

; --- Config ---
PopupTitle      := "Product Activation Failed"
LogDir          := EnvGet("ProgramData") . "\GraceKeeper\logs"
if !DirExist(LogDir)
    DirCreate(LogDir)
LogFile         := LogDir . "\dismisser.log"
DisabledFile    := EnvGet("ProgramData") . "\GraceKeeper\DISABLED"
LogMaxLines     := 500
PauseUntilTick  := 0

; Plan B: if event hook is unreliable, set USE_POLLING := true (and poll interval)
USE_POLLING     := false
PollMs          := 250

RockwellPathPrefixes := [
    "C:\Program Files (x86)\Rockwell Software\",
    "C:\Program Files\Rockwell Software\",
    "C:\Program Files (x86)\Common Files\Rockwell\",
    "C:\Program Files\Common Files\Rockwell\"
]

; --- Win32 constants ---
; EVENT_OBJECT_SHOW fires when a window becomes visible — by which time the title
; has been set. EVENT_OBJECT_CREATE fires at CreateWindowEx time, which is before
; AHK-Gui dialogs set their title (and possibly before some Win32 dialogs do too).
EVENT_OBJECT_SHOW          := 0x8002
WINEVENT_OUTOFCONTEXT      := 0x0000
WINEVENT_SKIPOWNPROCESS    := 0x0002
OBJID_WINDOW               := 0

Persistent()  ; SetWinEventHook (raw Win32) doesn't count toward AHK's stay-alive logic; polling SetTimer would but we want this active in either mode

; Startup probe: if we can't write the log file, die loud so the supervisor records it
try {
    FileAppend(FormatTime(A_Now, "yyyy-MM-dd HH:mm:ss") . " | startup probe OK`n", LogFile)
} catch as e {
    ExitApp(2)
}

PauseForMinutes(minutes) {
    global PauseUntilTick
    PauseUntilTick := A_TickCount + (minutes * 60 * 1000)
    LogLine("paused for " minutes " minutes")
}

IsPaused() {
    global PauseUntilTick
    return PauseUntilTick > A_TickCount
}

RotateLog()

; Keep callback alive for the life of the script
global HookCallback := CallbackCreate(OnWinEvent, "F", 7)
global HookHandle := 0

if (USE_POLLING) {
    SetTimer(PollForPopup, PollMs)
    LogLine("started (polling mode, " PollMs "ms)")
} else {
    HookHandle := DllCall("SetWinEventHook"
        , "UInt", EVENT_OBJECT_SHOW
        , "UInt", EVENT_OBJECT_SHOW
        , "Ptr",  0
        , "Ptr",  HookCallback
        , "UInt", 0       ; all processes
        , "UInt", 0       ; all threads
        , "UInt", WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS
        , "Ptr")
    if (!HookHandle) {
        LogLine("FATAL: SetWinEventHook failed; falling back to polling")
        SetTimer(PollForPopup, PollMs)
    } else {
        LogLine("started (event-hook mode)")
    }
}

OnExit((*) => HookHandle ? DllCall("UnhookWinEvent", "Ptr", HookHandle) : 0)

; Hook callback: HWINEVENTHOOK, DWORD event, HWND, LONG idObject, LONG idChild, DWORD eventThread, DWORD eventTime
OnWinEvent(hHook, event, hwnd, idObject, idChild, eventThread, eventTime) {
    global OBJID_WINDOW
    if (idObject != OBJID_WINDOW || !hwnd)
        return
    HandleCandidate(hwnd)
}

PollForPopup() {
    global PopupTitle
    hwnd := WinExist(PopupTitle)
    if (hwnd)
        HandleCandidate(hwnd)
}

HandleCandidate(hwnd) {
    global PopupTitle, DisabledFile
    if FileExist(DisabledFile)
        return
    if IsPaused()
        return
    try {
        title := WinGetTitle("ahk_id " hwnd)
    } catch {
        return
    }
    if (title != PopupTitle)
        return
    if (!IsRockwellProcess(hwnd))
        return
    DismissPopup(hwnd)
}

IsRockwellProcess(hwnd) {
    global RockwellPathPrefixes
    try {
        pid := WinGetPID("ahk_id " hwnd)
        exePath := ProcessGetPath(pid)
        for prefix in RockwellPathPrefixes {
            if (InStr(exePath, prefix) = 1)
                return true
        }
    } catch {
        return false
    }
    return false
}

DismissPopup(popupHwnd) {
    priorHwnd := WinGetID("A")
    if (priorHwnd = popupHwnd)
        priorHwnd := 0

    pid := 0
    exeName := "?"
    try {
        pid := WinGetPID("ahk_id " popupHwnd)
        exeName := ProcessGetName(pid)
    }

    priorTitle := ""
    if (priorHwnd) {
        try priorTitle := WinGetTitle("ahk_id " priorHwnd)
    }

    PostMessage(0x0010, 0, 0, , "ahk_id " popupHwnd)

    if (priorHwnd) {
        try WinActivate("ahk_id " priorHwnd)
    }

    LogLine("dismissed pid=" pid " (" exeName ") | restored focus to `"" priorTitle "`"")
}

LogLine(msg) {
    global LogFile
    stamp := FormatTime(A_Now, "yyyy-MM-dd HH:mm:ss")
    try FileAppend(stamp " | " msg "`n", LogFile)
}

RotateLog() {
    global LogFile, LogMaxLines
    if (!FileExist(LogFile))
        return
    content := FileRead(LogFile)
    lines := StrSplit(content, "`n", "`r")
    if (lines.Length <= LogMaxLines)
        return
    keep := []
    Loop LogMaxLines {
        keep.InsertAt(1, lines[lines.Length - A_Index + 1])
    }
    FileDelete(LogFile)
    for line in keep {
        if (line != "")
            FileAppend(line "`n", LogFile)
    }
}
