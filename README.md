# Taskbar AI & System Monitor (C# WPF)

Windows 작업표시줄 상단/트레이 영역에 밀착하여 **CPU / RAM 실시간 점유율** 및 **현재 로컬에 설치된 LLM(Codex, Claude, Gemini, Grok, Copilot, Ollama 등)**을 자동으로 탐색하여 아이콘과 정수+% 형태로 깔끔하게 표시하는 경량 데스크톱 위젯입니다.

---

## 📌 주요 특징

- **로컬 LLM 자동 탐색 및 노출 (Auto-Detection)**:
  - 시스템 PATH 및 각 에이전트의 홈 디렉터리(`~/.codex`, `~/.claude`, `~/.gemini`, `~/.grok`, `~/.copilot`, `ollama`)를 자동 탐색하여 현재 PC에 설치된 LLM만 위젯에 동적으로 노출합니다.
  - **Codex (ChatGPT Pro)**: 실시간 쿼터 / 남은 한도 및 리셋 주기
  - **Claude Code (Max)**: 실시간 7일 주간 쿼터 / 5시간 세션 사용량
  - **Antigravity (Gemini)**: 실시간 5시간 롤링 윈도우 잔여량 / 모델 정보
  - **Grok CLI (xAI)**: 인증 계정, 활성 세션 수, 상태 모니터링
  - **GitHub Copilot CLI**: 인증 사용자, 명령 이력, 활성 상태 모니터링
  - **Ollama**: 로컬 서비스 실행 여부 및 로드된 모델 목록
- **마지막 위치 기억 & 스마트 복원**:
  - 드래그하여 배치한 마지막 화면 좌표(`WindowLeft`, `WindowTop`)를 기억하여 재실행 시에도 해당 위치에 정확히 복원됩니다.
  - 더블 클릭 또는 메뉴 선택 시 작업표시줄 시작버튼 좌측으로 즉시 자석 스냅됩니다.
- **Git 기반 버전 관리 & 원클릭 업데이트 (`apply.bat`)**:
  - `agent-terminal-notifier`와 동일한 방식으로 Git 커밋 해시 기반의 버전 관리를 제공합니다.
  - 저장소에서 `apply.bat`을 실행하면 현재 HEAD 커밋을 감지하여 빌드/설치하고, 이미 동일한 커밋으로 설치되어 있다면 불필요한 재빌드를 건너뜁니다.
- **초경량 시스템 모니터링**:
  - **CPU**: Win32 `GetSystemTimes` 기반 (1.5초 주기)
  - **RAM**: Win32 `GlobalMemoryStatusEx` 기반 (1.5초 주기)
  - 작업표시줄 공간을 방해하지 않는 콤팩트 인디케이터 스타일

---

## 🚀 설치 및 실행 방법

### 방법 1. Git 기반 설치 및 업데이트 (`apply.bat`)
`apply.bat`을 더블 클릭하면 현재 Git 저장소 커밋을 기반으로 `%LOCALAPPDATA%\TaskbarAiMonitor`에 빌드 및 설치하고 즉시 실행합니다.
- 이미 최신 커밋이 설치되어 있으면 재빌드 없이 유지됩니다.
- 강제 재빌드가 필요할 경우 `apply.bat -Force`를 실행합니다.

### 방법 2. 간편 실행 (`run.bat`)
`run.bat` 더블 클릭 시 설치된 실행 파일을 실행하거나 로컬 빌드로 즉시 실행합니다.

### 방법 3. CLI 개발 실행
```powershell
dotnet run
```

---

## ⚙️ 설정 커스터마이징 (`config.json`)

위젯에서 **우클릭 → ⚙️ 설정 파일 열기**를 누르면 메모장으로 열립니다.  
(저장 경로: `%APPDATA%\TaskbarAiMonitor\config.json`)

```json
{
  "SystemPollIntervalMs": 1500,
  "AiPollIntervalSeconds": 300,
  "ShowRemainingQuota": true,
  "IsWeeklyMode": true,
  "WindowLeft": 1500.0,
  "WindowTop": 1030.0,
  "IsTopmost": true,
  "IsLocked": false,
  "SnapToStartButton": false,
  "EnabledAgents": null
}
```

* `EnabledAgents`: 특정 에이전트만 표시하고 싶을 경우 `["codex", "claude"]` 형태로 지정 가능 (`null`이면 설치된 전체 자동 탐색).
* `ShowRemainingQuota`: `true`이면 **잔여량(%)**, `false`이면 **사용량(%)**으로 표시.
* `WindowLeft`, `WindowTop`: 마지막으로 배치된 창 좌표가 자동 저장되어 다음 실행 시 복원됩니다.

---

## 🖱️ 마우스 조작법

| 동작 | 기능 |
|---|---|
| **좌클릭 드래그** | 위젯 원하는 위치로 이동 (설정에 좌표 자동 저장 & 다음 실행 시 유지) |
| **더블 클릭** | 작업표시줄 상단/시작버튼 좌측으로 자동 자석 스냅 |
| **우클릭** | 메뉴 (새로고침, 잔여/사용 전환, LLM 재탐색, 항상 위 고정, 업데이트 적용, 종료 등) |
| **트레이 아이콘 더블클릭** | 위젯 숨기기 / 보이기 토글 |
