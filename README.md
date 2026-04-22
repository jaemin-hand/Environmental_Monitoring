# Environmental_Monitoring

Windows 기반 환경 모니터링 프로그램의 초기 솔루션 골격입니다.

현재 구조

- `src/EnvironmentalMonitoring.App`: WPF 운영 화면 초안
- `src/EnvironmentalMonitoring.Worker`: 백그라운드 수집 서비스 뼈대
- `src/EnvironmentalMonitoring.Domain`: 장비/채널/샘플링 기본 모델
- `src/EnvironmentalMonitoring.Infrastructure`: 저장 경로와 파일 구조 기본값
- `docs/project-plan.md`: 초기 일정과 구현 단계 메모

현재 가정

- OS: Windows
- 통신: Modbus TCP
- 장비: Indigo520 1대, ADAM-6015 2대
- 채널: 온도 8, 습도 1, 대기압 1
- 기본 샘플링: 1분
- 알람 기준: 설정 온도 대비 ±5 degC

다음 구현 단계

- Modbus register map 반영
- SQLite 저장 및 일일 CSV 생성
- 실시간 그래프 바인딩
- 알람 이벤트 이력 저장
- 자동 시작 및 비정상 종료 복구
---
## 시스템 구조도
<img width="4337" height="2704" alt="Group 34" src="https://github.com/user-attachments/assets/acd9c6e5-7c33-4f9b-947b-56b2e87c4676" />


