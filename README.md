# military-supply-wpf

군수품 보급·개인장비 관리 WPF 프로그램입니다.
LIG The SSEN 임베디드 SW 스쿨 5기 WPF 팀 프로젝트로 만들었습니다. 데이터는 모두 가상입니다.

## 기본 사항
| 구분 | 내용 |
|---|---|
| 개발 인원 | 4명. C#을 처음 배웠으며, WPF는 모두 처음 학습 |
| 개발 언어·화면 기술 | C# 기반 WPF (MVVM) |
| 저장 | 로컬 JSON 파일, XLSX 가져오기·내보내기 |

## 팀원
| <img src="https://github.com/Hanyangsaja.png" width="80"> | <img src="https://github.com/hanincheol40.png" width="80"> | <img src="https://github.com/se71nny.png" width="80"> | <img src="https://github.com/bluebell0127.png" width="80"> |
|:---:|:---:|:---:|:---:|
| [Hanyangsaja](https://github.com/Hanyangsaja) | [hanincheol40](https://github.com/hanincheol40) | [se71nny](https://github.com/se71nny) | [bluebell0127](https://github.com/bluebell0127) |
| View | ViewModel | 디자인 | 데이터·서비스 |

## 역할 분배
| 담당 | 역할 | 주요 작업 |
|---|---|---|
| [Hanyangsaja](https://github.com/Hanyangsaja) | View 구조·화면 상호작용 | 메뉴와 화면 배치, 목록·입력 폼, 병사·장비 표시, 드래그 앤 드롭 입력 처리 |
| [hanincheol40](https://github.com/hanincheol40) | ViewModel·화면 상태 관리 | 데이터 바인딩, ICommand, 선택·검색·필터 상태, 지급 단계와 장비 투명도 연결 |
| [se71nny](https://github.com/se71nny) | 공통 디자인·화면 크기 대응 | 색상·글씨체·굵기, 공통 스타일·템플릿, 상태색, 창 크기 변경 시 배치·스크롤 검증 |
| [bluebell0127](https://github.com/bluebell0127) | 데이터·업무 처리 서비스 | 데이터 모델, 저장·조회, 재고 검증, 지급 확정·완료·반납 처리, XLSX 가져오기·내보내기 |

## 실행 환경
Windows, .NET 11 SDK, Visual Studio 2026
