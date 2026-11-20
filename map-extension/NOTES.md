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
