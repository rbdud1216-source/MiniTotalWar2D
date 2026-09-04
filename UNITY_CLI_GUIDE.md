# Unity CLI 사용 가이드 & 설치 리포트

> **설치 일시:** 2026-07-24  
> **버전:** `1.0.0-beta.2`  
> **설치 경로:** `C:\Users\PC\AppData\Local\Unity\bin\unity.exe`

---

## 1. Unity CLI 개요

Unity CLI는 Unity Hub 데스크톱 애플리케이션 없이 터미널/가상환경(CI/CD)에서 Unity 에디터, 모듈, 프로젝트, 인증, 빌드 및 테스트를 제어할 수 있는 독립형 명령어 인터페이스(Command Line Interface)입니다.

### 주요 장점
- **CI/CD 및 자동화 빌드 최적화:** Headless 환경에서 Unity 에디터 및 모듈 관리 가능
- **정형 데이터 출력 지원:** JSON, TSV, NDJSON 출력 형식을 지원하여 스크립트 연동 용이
- **빠른 프로젝트 및 에디터 관리:** 에디터 목록 확인, 설치, 모듈 추가, 프로젝트 실행 단축키 지원

---

## 2. 설치 및 환경 설정

### Windows (PowerShell) 설치 명령
```powershell
$env:UNITY_CLI_CHANNEL='beta'; irm https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.ps1 | iex
```

### macOS / Linux 설치 명령
```bash
curl -fsSL https://public-cdn.cloud.unity3d.com/hub/prod/cli/install.sh | UNITY_CLI_CHANNEL=beta bash
```

### 환경변수 (PATH)
설치 후 터미널을 새로 열거나 PATH에 `C:\Users\PC\AppData\Local\Unity\bin`을 추가하면 `unity` 명령어를 직접 사용할 수 있습니다.

---

## 3. 핵심 명령어 요약

| 기능 | 명령어 | 설명 |
| :--- | :--- | :--- |
| **버전 확인** | `unity --version` | 설치된 Unity CLI 버전 출력 |
| **환경 진단** | `unity doctor` | CLI 및 Unity 개발 환경 통합 진단 |
| **설치된 에디터 목록** | `unity editors` (또는 `unity editors -i`) | 로컬에 설치된 Unity 에디터 목록 및 경로 출력 |
| **에디터 설치** | `unity install lts` | 최신 LTS 에디터 설치 (예: `unity install 6000.3.7f1`) |
| **모듈 포함 설치** | `unity install lts -m android ios webgl` | 에디터 설치 시 타겟 플랫폼 모듈 동시 설치 |
| **모듈 추가** | `unity install-modules -e 6000.3.18f1 -m android` | 기존 설치된 에디터에 모듈 추가 |
| **프로젝트 열기** | `unity open ./MyProject` (또는 `unity ./MyProject`) | 해당 프로젝트에 설정된 버전의 Unity 에디터로 실행 |
| **로그인 상태 확인** | `unity auth status` | Unity 계정 로그인 상태 확인 |
| **로그인** | `unity auth login` | 브라우저 기반 로그인 실행 |
| **CLI 업그레이드** | `unity upgrade` | Unity CLI 최신 버전으로 자동 업그레이드 |
| **프로젝트 빌드** | `unity build ./MyProject` | 배치 모드로 프로젝트 빌드 실행 |
| **단위/플레이 테스트** | `unity test ./MyProject` | EditMode/PlayMode 테스트 실행 및 결과 보고서 생성 |

---

## 4. 로컬 테스트 결과

현재 컴퓨터에 정상 설치되어 아래 항목들이 검증되었습니다:

1. **CLI 버전:** `1.0.0-beta.2` (Windows x64)
2. **에디터 감지:** 로컬에 설치된 Unity 에디터 `6000.3.18f1` (`C:\Program Files\Unity\Hub\Editor\6000.3.18f1\Editor\Unity.exe`) 감지 완료
3. **진단 (`unity doctor`):** 정상 실행 확인