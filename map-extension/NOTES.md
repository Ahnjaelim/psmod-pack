# Map Extension — 설계 메모

목표: 외출 화면(지금은 장소 카드를 옆으로 넘기는 "교통" 창)을 Terminus 처럼 도시 지도로 바꾼다.
기존 Personal Mod Pack 과 별개의 모드로 만든다.

## 바닐라 구조 (디컴파일 기준, 0.46D)
- `TransitUI` (static): 외출 창. `CustomUIManager.CreateWindow("transit_system", ..., transitPrefab)`
  - `Open()` → `AfterhourLocation.GetDiscovered()` 로 장소 목록 → `ShowPage()` 가 카드(320x448)를 3장씩 배치
  - 카드 클릭 → `TransitUI.Select(location)`, 이동 버튼 → `MapUIManager.Instance.Travel(location)`
  - 카드 안: 장소 그림(`ResolveImage(ImageKey)`), 이벤트 목록(ScrollRect), 이용 불가 사유
- `MapUIManager`: `OpenUI/CloseUI`, `Travel(location)`, `Return()`. ModHook 의 OnAfterhourArrive/Leave Early/Late 발생
- `AfterhourLocation`: 장소 기본 클래스. 바닐라 = 쓰레기장, 매점, 잭슨 박사 (+ 모드 장소)
  - `IsDiscovered()`, `IsAvailable()`, `ActiveEvents`, `GetTitle()`, `ImageKey`
- `AfterhourEvents`: 장소별 이벤트(요일 고정: 월 매점 특별 재고, 수 쓰레기장, 금 재입고, 일 잭슨 할인 …)
- 모드 API: `ModHelper.AddAfterhourLocation(...)`, `AfterhourLocation.Register`

## 구현 방향 (안)
1. `TransitUI.Open` 을 가로채서(Prefix false) 지도 창을 띄운다. 이동은 그대로 `MapUIManager.Instance.Travel(location)`.
2. 지도 = 배경 이미지 1장 + 장소 아이콘(버튼). 장소 좌표는 장소 ID → (x, y) 표.
   - 모르는 모드 장소는 빈 자리에 자동 배치.
3. 아이콘: 이용 가능/불가 색, 이벤트 있으면 "!" 표시. 마우스 올리면 툴팁(이름, 이벤트, 남은 일수).
4. 선택 → 하단 정보 패널 + "이동" 버튼 (바닐라 카드 내용 재사용).
5. 2단계: 장소 사이 길(점선), 미확인 장소(?) 표시, 메모 등.

## 미정
- 지도 배경 그림: 직접 그린 PNG? 단색 + 격자? (Textures 폴더로 넣으면 모드 로더가 읽음)
- 장소가 지금 3~4곳뿐이라 지도가 휑함 → 장식용 건물(가게, 주택)을 배경에 그려 넣을지

## 0.1.0 (구현됨)
- `TransitMap.cs`: `TransitUI.Open` Postfix → 카드/선로/화살표 숨기고 같은 자리에 지도(1808x832) 삽입
  - 배경 `Map/map_background.png` (`ModHelper.ReadModFileBytes` 로 읽음; ModLoader 는 스캐너가 막음), 영역을 꽉 채우고 위아래 잘라냄
  - 마커: 클릭 = `TransitUI.Select`, 더블클릭 = `MapUIManager.Travel`, 빈 곳 클릭 = 선택 해제
  - 색: 청록 = 이용 가능, 노랑 = 선택, 회색 = 이용 불가, "!" = 이벤트 있음
  - 왼쪽 아래 정보 칸: 선택(없으면 마우스 올린) 장소의 이벤트, 효과, 남은 일수
  - 장소 좌표: `Positions` 표 (모르는 장소는 `Spare` 자리)
- 빌드: `dotnet build -c Release` → `Mods/MapExtension` 에 DLL, manifest, Textures 복사

## 0.1.4
- 아직 해금 안 된 장소(AfterhourLocation.GetAll 중 GetDiscovered 에 없는 것)는 이름 없는 어두운 네모로 표시, 클릭/호버 무시

## 0.2.0
- 배경 교체(2560x1440 남색 지도), 창 본문 전체(1852x972, 제목 줄 아래)를 덮음. 제목/닫기/이동 버튼은 지도 위로 올림

## 0.3.0
- "내 전당포" 마커 (0.55, 0.58, 주황): 선택 시 이동 버튼 활성(RefreshTravelButton Postfix), 이동/더블클릭 → 확인 창 → MapUIManager.CloseUI (외출 안 하고 가게로)

## 0.4.0
- 은퇴한 상인 8명 가게 자리(RetiredSpots): 잠긴 네모 + 회색 이름표, 클릭 없음 (기능은 나중에)

## 0.4.1
- 정보 칸 → 마우스 올린 마커 위 툴팁(ShowTip, 폭 420, 높이는 내용에 맞춤, 위에 자리 없으면 아래). 마우스 떼면 사라짐

## 0.4.3
- 네모 36 -> 24 (마커 상자 안 98,12), 테두리/이름표 배경 없음. 내 전당포는 설명 툴팁만, 클릭 동작 없음 (돌아가기 코드는 남아 있으나 HomeSelected 가 켜지지 않음)

## 0.5.0
- 세력 거점 4곳 잠긴 자리: 보안대 본부(0.52,0.20) 암시장(0.25,0.64) 혁명군 아지트(0.31,0.11) 카르텔 거점(0.70,0.89)

## 0.6.0
- RetiredMerchants.cs: 은퇴한 상인 8명 집을 AfterhourLocation(vme_retired_*)으로 등록 (ModHelper.AddAfterhourLocation, OnEnable/OnDisable)
  - 갈 수 있음 = 해금 && 오늘 %7 == 방문 요일. 화면은 잭슨 박사 무대/패널/카운터 재사용, 손님만 상인(게임 이름 키 + 초상화 함수), 판매 재고 없음, 잭슨 태블릿 숨김
  - 지도 툴팁: 설명 + 방문 요일 + 못 가는 이유

## 0.6.1
- 물장수/양식업자 배경: Map/bg_water_merchant.png, bg_aquafarmer.png. 도착 시 잭슨 무대(upgradeStoreScene)에서 가장 큰 SpriteRenderer 의 스프라이트를 교체(같은 월드 크기/피벗, Point 필터), 나머지 렌더러는 숨김. 떠날 때 복구. 로그에 무대 구조 출력

## 0.6.2
- 잭슨 무대는 SpriteRenderer 1개(inventor_scene / drjackson2)이고 잭슨이 그림에 포함됨. 배경 교체 시 상인 초상화를 별도 SpriteRenderer(VME_Merchant)로 세움: PersonX 0.476, PersonFootY 0.36(카운터 윗변), PersonHeight 0.42 (배경 비율)

## 0.6.3
- 상인 위치/크기 = 가게 손님 자리: StoreClientMono.SetImmediatlyArrive 후 image 의 위치/lossyScale/flipX 를 복사, 다시 HideClient. 실패 시 배경 비율(PersonX/FootY/Height)로 대체

## 0.7.0
- 상인 초상화 클릭 → 대사(Lines 랜덤, DialogUIManager.StartDialogue), 도착 시 인사 1회. BoxCollider2D + Interactable + MerchantPerson(TooltipInterface, IClickable) + TooltipContainer (잭슨 태블릿 방식)

## 0.7.1
- 상인 숨쉬기: 게임 IdleAnimator 를 붙이고 가게 손님(StoreClientMono)의 amplitude/frequency/axis 복사 (부모 스케일 차이 보정). 게임 옵션으로 idle 끄면 같이 꺼짐

## 0.7.2
- 전용 초상화 없는 상인(물장수/총기 장인: Lower, 고물상: Scav, 와인/목장: Farmer)은 세이브(runID)+Key 해시로 얼굴 고정. 바닐라만: spriteDictionary 의 "풀이름+번호" 연속 키 (GetPool 은 초상화 모드가 _psrl_ 얼굴을 덧붙여서 안 씀)

## 방향 (2026-11-20)
- 부동산: 보류(하지 않음). 은퇴한 상인: 나중에 천천히.
- 다음 큰 작업: 파밍 장소(터미너스 스타일: 테마 전리품, 고갈/회복, 위험도, 도구로 열리는 곳, 미확인 장소) → 작업장(숨겨진 인벤토리에 칸 저장, 밤 기계 가동 확인 필요)

## 0.8.0 파밍 테스트: 버려진 약국 (FarmLocation.cs)
- 자체 무대(카메라 화면 크기 어두운 판, sortingOrder 20) + 핫스팟 5개(네모, BoxCollider2D+Interactable+FarmHotspot)
- 클릭 → GenericConfirmationUIManager 확인 창(남은 시간 → 수색 후, 부상 위험) → 1.2초 후 결과
- 시간 = scavengingAttempts (도착 시 ResetScavenging, 쓰레기장과 같은 규칙; 물건마다 Cost), 부상/행운/불운 쓰레기장 규칙, 전리품 = 게임 표(medicalTable 등) → afterhourInventory
- 고갈: ModHelper.Get/SetAfterhourCounter(Id, "searched_<spot>") = 뒤진 날, RefillDays 후 회복. 금고는 crowbar/lockpicks 필요
- 지도 위치 (0.38, 0.38), 툴팁: 설명 + 뒤질 수 있는 곳 n/5

## 0.9.0 상자 창 (LootPopup.cs)
- 수색 성공 → 자체 오버레이 캔버스(sortingOrder 5000)에 상자 창: 칸마다 "?" 덮개, 클릭하면 공개(아이템 그림 = GameItemElement.ResolveSpriteByName, 단가로 등급 색)
- 가져가기/ESC/Enter → 전부(미공개 포함) afterhourInventory 로. 창 열린 동안 InputActionManager.Update 차단(Unbox Moment 방식)

## 0.9.1
- 핫스팟을 지도 마커 스타일로: 작은 네모(1080 기준 24px) + 월드 TMP 이름표(20px). 색: 청록=뒤질 수 있음, 주황=도구 필요, 어두움+회색 글자=이미 뒤짐. Area 는 가운데 점만 사용
- 위치: 바닥 가방(아래)·패널(오른쪽)을 피해 x 0.1~0.65, y 0.4~0.8

## 0.10.0 상자 = 진짜 인벤토리
- LootPopup 제거. 수색 성공 → DirectoryMaster.Item("lootbox_medium") 에 담아 contentWindow.Show()+Center(). 끌어서 가져가고 나머지는 남김
- 같은 외출 중 핫스팟 다시 클릭 → 남은 물건 있으면 상자 다시 열림. 장소 떠날 때(Leave) 상자 파괴(남긴 물건 사라짐)
- LootCover.cs: 아이템 middle 아래 "?" 덮개(Image+TMP). 클릭만 덮개가 받아 공개(등급 색 알림), 끌기는 부모 TreeNodeRender 로 → 덮인 채 이동 가능. 떠날 때 남은 덮개 제거

## 0.10.1 약국 배경
- Map/bg_farm_pharmacy.png (2560x1694). FarmLocation.BackgroundFile: 화면 cover(넘치는 위아래 잘림), Point 필터. 물건 Area 는 이제 그림 기준 좌표(imgW/imgH)
- 물건 이름/위치를 그림에 맞춤: 약장(왼쪽 벽 0.18,0.60) 조제실 카운터(0.53,0.57) 벽면 선반(오른쪽 0.78,0.66) 진열대(0.38,0.50) 지하실 문(0.33,0.72, 도구 필요)

## 0.10.3
- 테스트: FarmLocation.TestEnergy = 100 → 도착 시 scavengingAttempts = max - 100. **출시 전 0 으로**

## 0.10.4
- 이미 뒤진 곳 클릭 → 그 상자 다시 열기(남긴 물건, 없으면 빈 상자). 떠날 때 상자는 유지(창만 닫음), 다시 채워지는 날 클릭하면 버리고 새 수색. 상자는 메모리에만(게임 끄면 사라짐). 상자 안 아이템의 ? 덮개는 유지

## 0.10.5
- FarmSpot.Items ("id:가중치") + ItemChance(%): 한 개 굴릴 때 Items 에서 뽑을 확률, 아니면 Tables. 의료품 id 는 MedsItemDirectory 것만(pill_bottle 류는 UnusedDirectory 라 안 됨)
- 약장: 붕대3종/소독알코올/연고/각성제/항생제/분홍 주사 75%, 1~3개. 조제실 카운터: 알약류/붕대/화학 스캐너/수술 도구 55%. 벽면 선반: 붕대/알코올/연고/혈액팩 40%

## 0.10.6 이름표 선명하게
- 원인: 월드는 PixelPerfectCamera 로 저해상도 렌더 후 확대 → 월드에 그린 글자(TMP/월드 캔버스) 전부 뭉개짐
- 이름표를 게임 UI 층으로: afterhourWindow.handler 의 부모 맨 앞(SetSiblingIndex 0 = 모든 창 뒤)에 VME_FarmLabels, 월드 위치 → 화면 → 로컬 변환. Arrive 에서 생성, Leave 에서 삭제

## 0.10.8
- 테스트 모드면 이미 뒤진 곳도 상자가 비었으면 새로 수색(세이브에 뒤진 날이 남아 빈 상자만 열리던 문제). 상자에 안 들어간 물건은 외출 가방으로 + 로그 "rolled/in box/to bag"

## 0.10.9
- 상자 담기 수정: TryAcceptAll(box 아이템) 대신 CreateInventoryWindow 가 돌려준 GameInventory(boxInv)에 직접. 반환값(남은 개수)==0 이면 성공, 아니면 외출 가방. BoxHasItems/BoxItems 도 boxInv.childItems 기준

## 0.10.11
- "?" 회색(#8a8694). 클릭 → 코루틴으로 진행 막대(덮개 아래 3px, 민트) + "…" 깜빡임 후 공개. 시간: 기본 0.7s, 200+ 0.85s, 600+ 1.05s, 1500+ 1.3s

## 0.10.12
- 테스트 모드 재수색 예외 제거: 이미 뒤진 곳은 항상 상자만 열림(수색 창 없음)

## 0.10.13
- 상자 담기 실패 진짜 원인: 주운 물건은 IS_OWNED_TAG, 소유 표시 없는 상자는 거절(GeneralHelper.MayOwnershipAcceptItem). 수색 상자에 IS_OWNED_TAG

## 0.10.14
- 덮개를 타르코프식 빗금으로: 8x8 대각선 텍스처(Point, Repeat) Image.Tiled. "?"·"…" 제거, 진행 막대만 유지. 호버 시 살짝 밝게

## 0.11.0 바닥 물건 남기기
- FarmLocation.Leave → StashFloor: afterhourInventory(바닥) 물건을 장소별 보관 인벤(CreateInventoryWindow 30x20, 메모리)으로 Expel+TryAcceptAll. Leave 가 TransferAfterhourToInv(버림)보다 먼저 불림
- Arrive → RestoreFloor: 보관 물건을 바닥에 다시 깜(꽉 차면 보관함에 남김). GetLeaveWarning 문구 "바닥에 둔 물건은 …에 그대로 남겨 둔다"
- 게임 끄면 사라짐 (세이브 저장은 미구현)

## 0.12.0 세이브 DB (SaveDb.cs)
- 게임 내장 모드 저장소 ModHelper.Set/GetModData(→ playerStore.modData, 세이브와 함께 저장/되돌림)
- 키: farm/<장소>/floor (바닥 보관), farm/<장소>/box/<물건> (상자에 남긴 것). 값 = PlayerStore.EncodeSaveItem("save_bag", inv, window) JSON, 복원은 DecodeSaveItem(json, inv)
- 쓰기: PlayerStore.SaveGame Prefix(잠/F5). 읽기: ModHook.OnGameLoadedLate (메모리 상태 비우고 다시 채움)
- 안 깐 물건: 아이템 태그 VME_UNREVEALED_TAG (세이브에 같이 저장), 상자 열 때 EnsureCovers 로 빗금 복원. 공개/상자 밖 정리 시 태그 해제

- 0.12.1: 안 깐 물건(VME_UNREVEALED_TAG)은 툴팁 안 뜸 (GameItemElement.HasTooltip Postfix)
- 0.12.2: 숨김 판정은 item.state 로 (IsTag 는 modifiedState 라 skipValidate 로 켠 태그를 못 봄)
- 0.13.0: 지도 아이콘 (Map/icon_*.png 검은 실루엣 → 로드 시 흰색 변환 후 Image.color 틴트). 수색=zoom_in, 가게=cart, 전당포=home, 잠긴 곳/세력=네모. ! 표시는 아이콘 오른쪽 위
- 0.13.1: 수색 장소 안 뒤질 곳 = icon_box (월드 SpriteRenderer, 30px 기준)
- 0.13.2: 뒤질 곳 아이콘을 UI 층(이름표 층)에 Image 로. 월드 sr 은 숨기고 콜라이더로 클릭
- 0.14.0: 수색 장소 패널 제거 → 잭슨 박사 패널(UIUpgradeMerchantPanel, 돌아가기만). 기력 바: 바닥 창(afterhourWindow) 위쪽 가운데, 120x10 캔버스 단위, 민트→빨강
- 0.14.1: 기력 바 위치 = 바닥 창 가운데 (widthPixels/2), 165x12
- 0.14.2: 수색 상자 창 제목 = 뒤진 곳 이름 (PixelWindow.SetTitle)

## 수색처 해금 (계획, 인투더데드식)
- 시작 시 열린 수색처 2곳 (약국 + 1곳 미정). 나머지는 지도에 어두운 네모.
- 해금 조건: 그 장소의 모든 곳을 한 번 이상 뒤짐 (리필과 무관, 세이브 DB 기록)
- 조건 달성 → 장소 안에 망원경 지점(옥상/창문) 생김 → 기력 소모 → 미리 정한 근처 1~2곳 해금
- 막힘 방지 우회로(정보 구매/망원경 아이템) 검토
- 0.14.3: 임시 배경 Map/bg_placeholder.png (원래 "임시 배경.png"). 새 장소는 BackgroundFile 비워 두거나 파일 없으면 이걸 씀
- 0.14.4: 총포상.png → Map/bg_gunsmith.png, 은퇴한 총기 장인 배경으로
- 0.15.0: 미확인 장소 19곳 (TransitMap.UnknownSpots: key/실제 이름/단계/위치). icon_unknown 회색, 툴팁만. 만들면 FarmLocations 로 옮기고 목록에서 뺀다
- 0.16.0: 팝업(수색 확인, 전당포로 돌아가기)을 폐품 수집 창(UIScavengingPanel) 복제 스타일로 (StyledDialog). 게임 스크립트 제거 후 Button 재연결, 실패 시 기본 확인 창
- 0.16.1: 폐품 수집 창 복제 팝업 되돌림 (보기 이상함) → 기본 확인 창. StyledDialog 삭제
- 0.16.2: 미확인 장소 아이콘/라벨 투명도 0.2
- 0.16.3: 지도 마커(아이콘+라벨) 0.7배 (아이콘 가운데 기준 축소, MarkerScale). 툴팁 위치는 실제 마커 영역 기준
- 0.16.4: 구역 글자 표시 (상류층 구역 0.63,0.11 / 하층민 구역 0.36,0.93), 흐린 글자, 마커 뒤
- 0.17.0: 분점 (방치형, Branches.cs). 부지 3곳(하층민 6000/120~250, 시내 15000/300~550, 상류층 40000/800~1300). 지도 icon_stock 클릭 → 확인 → 구매. PlayerStore.BeginDay Postfix 로 매일 수입(ModHelper.AddCredit), last_paid_day 로 중복 방지. DB: branch/<key>="1|번 돈|오늘 수입"
- 0.18.0: 싸돌아다니기 (Roaming.cs). 장소에서 돌아가기 → 가게 암전 끝나면 지도 자동으로 다시 열림(OnAfterhourLeaveLate). 내 전당포 골라야 가게에 남음. OnArriveStore Postfix 로 visitLeftTonight 유지 → 문으로도 다시 나갈 수 있음
- 0.18.1: 장소 → 지도 직행. 돌아가기 암전 안(OnAfterhourLeaveLate)에서 TransitUI.Open + OnLeaveStore + 셔터 닫기 (OpenUI 안쪽과 같음) → 가게 화면 안 거침
- 0.19.0: 외출 중 체력 바 (HealthBar.cs). 체력=(10-woundState)*10, 바닥 창 위 가운데, 모든 장소. 40 이하 (중상), 불안정 표시. 수색처 기력 바는 그 위로
- 0.20.0: 체력/기력 바 → OutingHud (화면 왼쪽 아래 고정, 오버레이 캔버스 1920x1080 기준). 지도 열림 또는 장소에 있을 때 표시. 수색처 기력 바/HealthBar 제거
- 0.21.0: 산 분점은 들어갈 수 있음 (BranchLocation : FarmLocation, 임시 배경, 바닥 보관 farm/branch_<key>/floor, 도착 시 수입 알림, 기력 안 채움). 지도: 안 산 부지 클릭=구매, 산 분점=선택/더블클릭 이동
- 0.21.0: 기력은 하룻밤 한 번만 채움 (ScavHelper.ResetScavenging Prefix, 싸돌아다니기로 무한 충전 방지)

## 나중에 조정 (보류)
- 기력(수색 횟수, PlayerStore.scavengingAttempts) 밸런스 전반: 최대치, 장소별 비용, 하룻밤 1회 충전 규칙(Roaming.EnergyOncePatch), 세이브 다시 불러오면 재충전되는 점, TestEnergy=100(출시 전 0)
- 0.22.0: 분점 시뮬레이션. 고정 수입 제거 → 본점 "분점" 버튼(BranchPanel, 왼쪽 아래 오버레이)으로 가상 재고(8x6, branch/<key>/stock)에 넣으면 매일 아침 BranchSim: 위치별 손님 수/종류(10종, 예산+선호 아이템 종류), 감정가×배율로 최대 2종 구매, 로그 최근 7일(branch/<key>/log)
- 0.22.1: 지도에서 여는 확인 창이 지도 뒤에 숨던 문제 → Dialogs.Confirm (확인 창/어둠 판에 Canvas overrideSorting 으로 지도보다 위). HUD 맨 위 소지금
- 0.23.0: 확인 창 = 게임 외출 확인 창(MapUIManager.confirmationPanel) 복제 (Dialogs.Confirm): 본문/버튼 글자·동작만 교체, 지도 위 층. 실패 시 기본 확인 창. 수색/분점 구매/전당포 복귀
- 0.24.0: 지도 [이동] 왼쪽에 [돌아가기] (이동 버튼 복제, 누르면 확인 없이 CloseUI = 가게로)
- 0.24.1: 확인 창 복제본을 지도가 열려 있으면 지도(TransitUI.Root) 안에 넣음 (다른 캔버스라 정렬값으로는 못 올림) + 자체 어둠 판. 버튼 글자는 버튼 영역 안 글자로 찾기 (오른쪽 No 안 바뀌던 문제)
- 0.24.2: 지도 버튼 정리: 이동/돌아가기 너비 0.55배, [이동] 왼쪽 [돌아가기] 오른쪽, 묶음 가운데 = 원래 이동 버튼 자리. HUD 는 지도 열려 있으면 지도 그림 영역 안 왼쪽 아래
- 0.24.3: 마커 y 최대 0.86 (아래 버튼/HUD 줄 피함), 하층민 구역 글자 0.28,0.88
- 0.25.0: 분점 모니터 (BranchMonitor): 게임 달력 모니터 창(AdvCalendarUIManager.UIPanel) 복제, 탭 3개 = 분점 3곳, 내용 = 요약/[재고 넣기]/최근 기록. [분점] 버튼이 이걸 엶
- 0.25.1: 분점 모니터 내용을 베이지 종이(창보다 작은 가장 큰 이미지) 안, 탭 아래에 배치. 어두운 글자, 구분선, [재고 넣기] 오른쪽 위
- 0.26.0: 분점 재고 자동 채우기 (모니터 [자동 채우기]): 본점 invElement 에서 고정/남의 것/용기/기계 제외, 그 분점 손님 선호 점수(BranchSim.Score) 높은 순, 가치 순으로 재고 찰 때까지
- 0.26.1: 분점 모니터 내용 칸을 창 그림(가장 큰 이미지) 기준 비율(종이 여백 6.5%/9.8%)로. 분점 재고 17x10 (storage_bay_large 와 같음)
- 0.26.2: 분점 판매가는 원래 이벤트 시세 포함(GetCurrentValue includeEvents). 기록에 "이벤트 +N%" 표시
- 0.26.3: 분점 판매가에 소매 마진 적용 (GetCurrentValue useRetailMarkup=true, 가게 진열가와 같은 계산)
- 0.27.0: 분점 손님에 세력 평판(빈도 × clamp(1+평판/200, 0.25~2)) + 본점 매력도(손님 수 × clamp(매력도/400, 0.6~1.8)). 모니터에 손님 구성 %, 기록에 매력도 표시. 자동 채우기 점수도 평판 반영
- 0.27.1: 밀수품(CONTRABAND_ITEM_TAG/종류 CONTRABAND)은 어느 분점에서도 안 팔림 (재고에 넣을 수는 있음). 자동 채우기도 제외
- 0.28.0: 분점 매일 자동 채우기 토글 + 종류 필터 8개(식품/물·음료/술·약물/의약품/무기/부품/재료/사치품). 아침 시뮬레이션 전에 AutoFill, 기록에 "아침 자동 채우기: N개". DB branch/<key>/auto = "1|비트"
- 0.29.0: 분점 자동 채우기 설정 창 (BranchRestockWindow, 게임 호퍼 창과 같은 CustomUI 기계 스타일): 매일 자동 채우기 토글, 필터 3모드(전부/허용 목록만/차단 목록 제외), 이름 검색으로 목록 추가(ItemIndex: 전체 아이템 이름표를 프레임 나눠 1회 생성). 종류 버튼 8개 제거. DB auto = "1|m모드|id,id"
- 0.29.1: 자동 채우기 목록 추가를 호퍼식으로 (UseSearch=false: [아이템 추가] → 가게 물건 클릭, 우클릭/Esc 끝, ItemPinHelper.BlocksClicks Postfix 로 클릭 막음). 검색 코드는 UseSearch=true 로 되살림
- 0.29.2: 싸돌아다니기 배낭 유지 (떠날 때 afterhourPocketSlotInvBackpack 의 배낭 기억 → 지도 열 때 selectedBackpack 다시 지정)
- 0.29.3: 자동 채우기가 본점 인벤토리 안 보관함/상자/냉장고(CONTAINER_TAG, STORAGE) 속까지 찾음. 작업 기계 안, 고정한 용기 안은 제외
- 0.29.4: 분점 설정 "가격 하락 이벤트 중인 물건은 안 팔기" (EventPercent<0 이면 손님 후보에서 뺌). DB auto 4번째 h0/h1
- 0.29.5: 고르기 모드 빈 곳 안내 툴팁 제거 (물건 위에서만)
- 0.30.0: 분점 모니터 끌어서 이동(DragWindow), 설정 창 열어도 모니터 유지(설정 창을 위 층으로 RaiseAbove), 고르기 중엔 모니터 잠깐 숨김, 설정 바꾸면 모니터 요약 갱신
- 0.30.1: [재고 넣기] 눌러도 모니터 유지, 재고 창을 모니터 위로 (RaiseAbove)
- 0.30.2: 분점 모니터 뒤 아이템 툴팁/클릭 막기 (모니터 그림에 빈 TreeNodeRender → RenderHandler.RaycastElement 가 거기서 멈추고 null)
- 0.30.3: 분점 모니터 누르고/끄는 동안 UIDragHandler.isDraggingAnyUI=true (아이템 범위 선택 네모 안 생기게)
- 0.30.4: [분점] 버튼을 게임 가게 UI 캔버스(달력 창 부모) 맨 뒤로 옮김 → 로딩/메뉴 화면 위로 안 튀어나옴. 1920x1080 기준 크기 → localScale 로 맞춤
- 0.30.5: 분점 재고 창(RaiseAbove 로 올린 창) 위로 아이템 끌면 안 보이던 문제 → 게임 오버레이 층(RenderHandler.overlayRoot, 끄는 아이템/툴팁)도 그 창보다 위 정렬값으로 (RaiseOverlay)
- 0.30.6: [분점] 버튼을 게임 아이템 창 캔버스(windowUICanvas)의 창 루트(RenderHandler.windowRootObject) 바로 앞으로 → 가게 물건/창 뒤 배경 층 (예전엔 가게 UI 캔버스라 물건 위에 그려짐). 없으면 예전 자리
- 0.30.7: [분점] 버튼이 매 프레임 창 루트 바로 앞 순서 유지 (KeepBehindWindows) → 게임이 형제 순서를 바꿔도 안 사라지게 (0.30.4 에선 ESC 연타로 가려짐)
- 0.31.0: [분점] 버튼 오른쪽에 산 분점마다 [<구역> 재고] 바로가기 (누르면 그 분점 재고 창, 모니터 열려 있으면 그 위로)
- 0.31.1: 우클릭/정렬 메뉴가 안 눌리던 문제 (0.30.5 RaiseOverlay 가 오버레이 루트 안에 Canvas 를 끼워서 ItemContextHandler 의 루트 GraphicRaycaster 에 안 잡힘) → 끼운 캔버스 대신 오버레이 루트 캔버스(RenderHandler.OverlayCanvas) sortingOrder 만 올림
- 0.31.2: 분점 기록에 손님 소지금(그날 예산) + 빈손 이유 (마음에 드는 게 없음 / 너무 비쌈: 제일 싼 게 N ₵)
- 0.32.0: 분점 수요에 시세 이벤트 반영: (1) 고를 때 가중치 clamp(1+이벤트%/50, 0.3~4) (2) 상승 이벤트 물건엔 예산 ×(1+%/100)까지 씀 (3) 유행: +10% 이상 이벤트 물건은 안 좋아하는 종류여도 손님마다 확률 min(0.5, %/100)로 관심 (기록에 "유행"). 이벤트 % 는 하루 한 번 캐시
- 0.33.0: 분점 모니터를 웹앱식으로: 위 탭 [전체]+분점 3곳 (탭 하나 복제, 원래 세 탭 폭에 4등분 LayoutTabs), 왼쪽 사이드바 메뉴 재고/판매 기록/손님/통계/설정 + 요약, 오른쪽 제목+도구 버튼+상태 줄+표 머리줄+스크롤 표(ScrollRect+RectMask2D). 재고 표(아이콘/이름/종류/수량/판매가/이벤트/좋아하는 손님, 정렬 4종), 기록(빈손만 필터, 전체는 날짜순 합침), 손님 표(비율/소지금/평판), 통계(로그 파싱: 날짜별 매출 막대, 많이 팔린 물건 10), 설정 요약 + [설정 바꾸기]=기존 설정 창. 안 산 분점 탭은 흐리게
- 0.33.1: 분점 모니터 판매 기록도 표로 (날짜 머리줄 + 손님 한 줄씩: 손님/소지금/산 것/금액/비고). ParseLine 이 기록 글을 읽음 (예전 형식 소지금 없음도)
- 0.33.2: 분점 모니터 재고 표: 같은 물건(분점/이름/판매가/이벤트) 한 줄로 묶어 수량 합, 종류별 묶음 머리줄(가지 수/개수/재고 가치), 묶음 순서도 정렬 기준 따름. 종류 열 제거
- 0.33.3: 분점 모니터 사이드 메뉴/도구 버튼을 윈도우 98 스타일(Win98Button: 회색 면 + 2단 입체 테두리, 누름/선택 시 뒤집힘)로. 위 탭은 그대로 게임 탭
- 0.33.4: 사이드 메뉴 버튼 높이를 도구 버튼(재고 넣기)과 같게, 위에서부터 촘촘히
- 0.33.5: 분점 모니터 표 숫자 열(재고/판매가/이벤트/소지금/금액/비율/평판) 오른쪽 정렬(NumCell, 머리줄은 ">" 표시), 숫자에 천 단위 쉼표(N, 기록 머리줄은 Commas)
- 0.33.6: 분점 모니터 종류 이름을 게임 번역(TypeHelper.GetTypeDisplayName) 우선으로 (household_good 같은 원문이 그대로 나오던 문제), 없으면 내 이름표, 그래도 없으면 "기타". 캐시
- 0.33.7: 전체 탭 판매 기록 머리줄에 "<color=#8a2,416>" 이 그대로 보이던 문제 (쉼표 넣기가 색 태그 숫자까지 건드림) → 쉼표를 색 태그 붙이기 전에
- 0.34.0: 판매 기록 표 "산 것" 칸을 아이템 그림(+×수량)으로 (ItemsCell). 기록엔 이름만 있으므로 이름 → ID: 이번 실행에서 판매/재고 때 기억한 BranchSim.NameToId → ItemIndex.IdByName(전체 아이템 이름표, 기록 화면 열 때 만들기 시작, 다 되면 모니터 다시 그림) → 뒤 괄호 뗀 이름. 못 찾으면 글자
- 0.34.1: 판매 기록 아이템 그림에 마우스 올리면 이름(+×수량) 툴팁 (HoverName: 모니터 안에 따로 그리는 작은 판, 마우스 따라감)
- 0.34.2: 약국 지하실 문 잠금 조건: 지렛대/자물쇠 따개 → 의무부 키카드(med_keycard) 또는 사령부 키카드(cmd_keycard). 소모는 안 함
- 0.34.3: [분점] 옆 분점별 재고 바로가기를 상자 그림 버튼(Map/img_box.png, 62x64)으로 + 아래에 분점 이름. 그림 못 읽으면 예전 글자 버튼
- 0.34.4: 상자 재고 버튼 다시 누르면 그 재고 창 닫기 (PixelWindow.IsVisible → Hide)
- 0.35.0: 판매 기록을 하루씩: 도구 줄에 [<] N일차 [>] (logDay, 기본 가장 최근, 분점 탭 바꿔도 같은 날 유지 / 없으면 최근). 상태 줄에 7일 중 몇 번째 날
- 0.35.1: 아이템 끌기 안 되던 문제: RaiseOverlay 가 오버레이 루트 캔버스를 한 번 올리면 끝까지 그대로라 오버레이가 마우스를 먼저 받음 → 올린 창(재고/설정)이 열려 있는 동안만 올리고 다 닫히면 원래 순서로 (TickOverlay, BranchPanel.Update). 올린 창 순서도 max+10 → 모니터+1. 처음 한 번 캔버스 순서 로그
- 0.35.2: 기록 표 아이템 그림 미리 찾기: 세이브 불러올 때(Branches.Read 끝) BranchMonitor.Prewarm 코루틴이 기록 속 이름 그림을 프레임마다 몇 개씩 캐시. 이름→ID 를 세이브(branch/names)에 저장해서 다음부턴 전체 아이템 목록(631개 생성) 없이. 모르는 이름이 있을 때만 목록 생성. 탭 열 때 목록 만들기 시작하던 것 제거
- 0.36.0: 분점마다 자체 매력도 (BranchSite.Attractiveness, 처음 0, branch/<key> 4번째 값). 손님 수 배율이 본점 매력도 대신 이것 (0 → 최저 0.6). 올리는 방법은 아직 없음
- 0.37.0: 와일드 네트워크 Advanced(엔드 콘텐츠) 탭에 분점 개보수 I~III (분점마다 3개, BranchRenovation, VME_BRANCH_RENOVATION_<KEY><n>). 단계마다 분점 매력도 +150 (본점 개보수와 같음). 비용 = 본점 500/750/1000 × 개설비/6000 (50 단위). 보너스는 산 업그레이드 수로 계산 (IsNetworkUpgradeUnlocked, 따로 저장 안 함)
- 0.37.1: 분점 상자 버튼 이름 글자 없애고 마우스 올리면 툴팁(HoverName, 툴팁 판은 자체 Canvas 로 맨 위). 상자 62x64 → 84x87
- 0.37.2: 약국 지하실 문을 열면 키카드 1장 소모 (FarmSpot.ConsumesTool, NeedsAny 앞 순서: 의무부 먼저). 확인 창에 "~ 1개를 쓴다", 쓰면 알림
- 0.37.3: 확인 창 복제본: 번역 컴포넌트(UnityEngine.Localization)도 지움 (오른쪽 버튼이 "No" 로 되돌아가던 문제), 원본 창 글자 폰트를 같은 자리 글자에 복사 (게임 폰트 교체 스크립트가 지워져 영어 폰트 + 한글 대체 글꼴이 섞이던 문제)
- 0.38.0: 수색처 19곳 추가 (FarmLocationData.cs, 미확인 장소 자리 그대로, 배경 전부 임시 배경). 시작 = 약국 + 철물점. 장소의 뒤질 곳을 다 한 번씩 뒤지면 망원경 지점(오른쪽 위, 기력 2) → 쓰면 Chain 장소 해금 (afterhour counter vme_farm_<key>/unlocked=1). 4단계는 끝. 못 찾은 곳은 지도에 미확인 장소로. 잠긴 곳: 지렛대(소모 안 함) 또는 부서별 키카드(소모)
- 0.39.0: 뒤질 곳 "남은 물건 %" (FillPercent): 안 뒤졌으면 100%, 뒤지면 0% → 7일 그대로 → 하루 4%씩 최대 40%. 5% 이상이면 다시 뒤질 수 있고, 다시 쌓인 건 잡동사니 표 75% / 생활용품 표 25%, 1~2개, 빈손 확률 = 기본 + (40-남은%)×1.2. 툴팁/수색 확인 창/지도 정보 칸에 표시. FarmSpot.RefillDays 는 이제 안 씀
- 0.40.0: 초반 수색처 밸런스 (FarmLocationData.Early: 약국, 철물점 + 2단계 6곳): 큰 배낭 72칸 + 하룻밤 기력 5 기준 이틀. 장소 기력 합 10 (원래 비율대로 나눔), 처음 뒤질 때 기력 1당 3~4개, 빈손 최대 5%. 수색 상자 6x4 → 8x8
- 0.40.1: 테스트 기력 끔 (TestEnergy = 0, 게임 기본 기력 사용)
- 0.40.2: 확인 창 글자에 게임 ScriptFontSwap.Apply (한글이면 게임 한국어 폰트) — 원본 폰트 복사로는 안 고쳐짐. 버튼 안 글자 칸 전부 바꿈 ("No" 남던 문제)
- 0.40.3: 윈도우 98 버튼 면 색 #c0c0c0 → #a8aea5 (선택 #c4cac1)
