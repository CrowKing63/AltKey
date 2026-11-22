# 재질 후보 조사 — 메탈(알루미늄)·우드(오크) 외 소재

- 상태: 조사 완료 — v6 패브릭 채택 → v7 구리 교체 → **v8 제4 카테고리 제거 (구리는 잔존 후보)**
- 관련 문서: `plan.md` (재질 스펙 — 현재 플라스틱/메탈/우드 구현)
- 목적: 재질을 확장할 때 쓸 **후보 소재**와, 각각 **라이트/다크에 적용할 요소**(톤 방향·테두리 행동·시작 색값)를 수집한다.

## 0. 선택 기준 (plan.md 범위 유지)

1. 텍스처 없이 **플랫 브러시로 표현 가능**해야 한다 (v1 결정 — 재질은 톤+테두리/그림자 세기 차이).
2. 기존 3개(플라스틱·메탈·우드)와 **한눈에 구분**되어야 한다 — 같은 계열의 미세 차이는 "차이가 거의 없다"는 첫 피드백을 재현한다.
3. 오버라이드 키는 plan.md §3.2의 7개(`KeyboardBg`, `KeyBg/Hover/Pressed`, `KeyBorder`, `HeaderBarHoverBg/PressedBg`)만 사용.

## 1. 실물 레퍼런스 (조사 요약)

| 소재 | 실제 제품·레퍼런스 | 시각적 정체성 (출처별 표현) |
|---|---|---|
| 탄소섬유 | Drop CTRL+ 탄소섬유 플레이트, BMW M·Porsche 카본 트윌, 게이밍 하드웨어 | "Techy, Stealth" — 자연색은 거의-블랙의 중립 회색 `#262626`~`#2E2E2E`, 2×2 트윌 직조 패턴. 라이트에서는 회색 크로스해치로 읽힘 |
| 티타늄 | 항공기·고급 펜시계, grade 5 | "muted, premium" — `#878681`/`#888880` (중립 미디엄 그레이에 미세한 올리브/브라운 힌트). 다크 변종은 건메탈 `#2A3439` |
| 유리·세라믹 | Apple(순백 "titanium dioxide" 광택, 세라믹 글래스 백), frost `#f5f5f7`, carbon `#1d1d1f`; Ducky Glass 에디션(투명) | 가장 "깨끗한" 소재 — 순수 흰색+hairline 테두리(거의 무테두리), 광택. 다크는 smoked glass/onyx 계열 |
| 패브릭·텍스타일 | Bang & Olufsen Beosound, Teenage Engineering(텍스타일·가죽), 무염 리넨 `#FAF0E6`/`#F6F1EB`(따뜻한 오프화이트), oatmeal/taupe | 가장 "조용한" 소재 — 탈포화 부드러운 중립색, 낮은 명도 대비, 날카로운 모서리 없음 |
| 구리·브론즈 | 고급 펜·건축 액센트, brushed copper `#C77826`, copper `#B87333`, dark bronze `#804A00` | "warm metallic" — 유일한 실제 색상(오렌지-코퍼) 소재. 다크는 espresso-bronze + 코퍼 하이라이트 |
| 마그네슘 합금 | Logitech G915 등 | "Sleek, Minimalist" — 알루미늄과 시각적 차이가 너무 작음 (→ 제외) |
| 레진·합성수지 | 프리미엄 커스텀 빌드 | 투명/반투명 디자인 가능 — v1 플랫 브러시 범위 밖 (→ 제외) |

참고: 키보드 케이스 소재 시장 조사(KBDfans, mechkeyfoundry 등)에서 알루미늄/플라스틱/PC가 95%를 차지하고, 탄소섬유·목재(메이플/월넛)·마그네슘·레진이 프리미엄 변주로 확인됨.

## 2. 후보 재질별 적용 요소 + 시작 색값 (제안)

기존 대비: 플라스틱 라이트 하우징 `#CCE8E8EC`, 다크 `#CC1E1E1E`. 각 후보는 "컨셉 한 줄"으로 구분된다.

### 2.1 탄소섬유 (`carbon`) — "테크니컬, 스텔스"

- **톤**: 순수 중립 회색(블루 시프트 없음). 메탈(차가운 블루-그레이)과 대비되는 핵심 차이.
- **테두리**: 또렷하지만 차분 (크리스프 네이utral).
- **라이트**: 실버-그레이 직조 — 하우징을 플라스틱보다 확실히 진한 중립 그레이로.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CCB4B7BC` (중립 미디엄 실버) |
| KeyBg | `#FFF6F6F7` (중립 거의-흰색) |
| KeyBgHover | `#FFC9CBCF` |
| KeyBgPressed | `#FFB2B5BA` |
| KeyBorder | `#3D000000` (크리스프 중립) |
| HeaderBarHoverBg | `#FFD8DADF` |
| HeaderBarPressedBg | `#FFC4C7CD` |

- **다크**: 탄소섬유 자연색(`#262626`) 기반 — 메탈 다크(스페이스 그레이, 밝고 블루)보다 진하고 순수한 블랙-그레이.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CC262628` (중립 탄소) |
| KeyBg | `#CC3A3A3C` |
| KeyBgHover | `#CC4E4E50` |
| KeyBgPressed | `#FF313133` |
| KeyBorder | `#29FFFFFF` (크리스프) |
| HeaderBarHoverBg | `#CC5F5F61` |
| HeaderBarPressedBg | `#CC717173` |

### 2.2 티타늄 (`titanium`) — "무뎌진 프리미엄 그레이"

- **톤**: 알루미늄의 또렷한 실버가 아니라, 올리브/브라운 힌트의 무디한 그레이 (grade 5 `#878681` 계열).
- **테두리**: 메탈보다 한 단계 부드러운 강화.
- **라이트**: 원형(untreated) 티타늄 — 따뜻한 무디한 그레이-토프.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CCB3B2AB` (따뜻한 무디 그레이) |
| KeyBg | `#FFFAF9F6` (따뜻한 거의-흰색) |
| KeyBgHover | `#FFC6C4BD` |
| KeyBgPressed | `#FFAFACA3` |
| KeyBorder | `#38000000` |
| HeaderBarHoverBg | `#FFD7D5CE` |
| HeaderBarPressedBg | `#FFC2BFB6` |

- **다크**: 건메탈(`#2A3439`)을 티타늄답게 미세하게 따뜻하게 보정.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CC33312E` (따뜻한 건메탈) |
| KeyBg | `#CC47453F` |
| KeyBgHover | `#CC5B5950` |
| KeyBgPressed | `#FF3D3B36` |
| KeyBorder | `#26FFFFFF` |
| HeaderBarHoverBg | `#CC6A685F` |
| HeaderBarPressedBg | `#CC7C7A70` |

### 2.3 유리·세라믹 (`glass`) — "가장 깨끗한 표면"

- **톤**: 가장 밝고 광택감 있는 소재 (Apple frost/순백). 키캡은 순수 흰색.
- **테두리**: **모든 재질 중 가장 약한 hairline** — 무접선 유리 느낌. 이게 메탈(강화)과 정반대인 차별점.
- **라이트**: frost 화이트 + 거의 없는 테두리.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CCEFF1F4` (밝은 frost, 플라스틱보다 밝고 차가움) |
| KeyBg | `#FFFFFFFF` (순수 흰색) |
| KeyBgHover | `#FFE8EBEF` (매우 연하게) |
| KeyBgPressed | `#FFD9DEE4` |
| KeyBorder | `#0A000000` (hairline — 무테두리에 가까움) |
| HeaderBarHoverBg | `#FFECEFF3` |
| HeaderBarPressedBg | `#FFDFE3E8` |

- **다크**: smoked glass / onyx — 거의-블랙의 차가운 유리.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CC141517` (onyx) |
| KeyBg | `#CC26282B` |
| KeyBgHover | `#CC3A3C40` |
| KeyBgPressed | `#FF1D1F21` |
| KeyBorder | `#14FFFFFF` (hairline 밝음) |
| HeaderBarHoverBg | `#CC4A4C50` |
| HeaderBarPressedBg | `#CC5B5D61` |

### 2.4 패브릭·텍스타일 (`fabric`) — "가장 조용한 표면"

- **톤**: 탈포화 따뜻한 중립 (무염 리넨 `#FAF0E6`/`#F6F1EB`, oatmeal). 우드의 꿀색보다 덜 포화, 더 "부드러운 회색-베이지".
- **테두리**: 가장 부드럽게 완화 — 날카로운 모서리 없음.
- **라이트**: 리넨 에크루 — 크림이 아니라 "뭉근한 오프화이트".

| 키 | 값 |
|---|---|
| KeyboardBg | `#CCF2EDE3` (리넨 오프화이트) |
| KeyBg | `#FFFDFBF7` (따뜻한 흰색) |
| KeyBgHover | `#FFEAE5DC` (부드러운 베이지) |
| KeyBgPressed | `#FFDCD5C8` |
| KeyBorder | `#1F3A332B` (아주 연한 따뜻한 그레이-브라운) |
| HeaderBarHoverBg | `#FFEDE9E0` |
| HeaderBarPressedBg | `#FFE0DACE` |

- **다크**: 찰콜 직조 — 우드 다크(포화 갈색)보다 덜 포화된 부드러운 찰콜.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CC262421` (따뜻한 찰콜) |
| KeyBg | `#CC38352F` |
| KeyBgHover | `#CC4C4840` |
| KeyBgPressed | `#FF322F29` |
| KeyBorder | `#16FFFFFF` (부드러운 밝음) |
| HeaderBarHoverBg | `#CC5A564D` |
| HeaderBarPressedBg | `#CC6C685E` |

### 2.5 구리·브론즈 (`copper`) — "따뜻한 금속 (유일한 색상)"

- **톤**: brushed copper(`#C77826`)에서 색채만 살린 분홍-실버(라이트) / espresso-bronze(다크). 다른 후보가 중립계열인 것과 달리 **오렌지-코퍼 시프트가 있는 유일한 재질**.
- **테두리**: 코퍼-브라운 중간 세기.
- **라이트**: 브러시드 코퍼 하우징 — 분홍빛이 도는 따뜻한 실버.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CCD8C5B0` (따뜻한 분홍-실버) |
| KeyBg | `#FFFDF9F4` (따뜻한 흰색) |
| KeyBgHover | `#FFE5D5C4` |
| KeyBgPressed | `#FFD2BC9E` |
| KeyBorder | `#3F4A3319` (코퍼-브라운) |
| HeaderBarHoverBg | `#FFEFE2D2` |
| HeaderBarPressedBg | `#FFE2CEB4` |

- **다크**: dark bronze / copper noir — 오크 다크와 구분되도록 오렌지 쪽으로 보정.

| 키 | 값 |
|---|---|
| KeyboardBg | `#CC382517` (espresso-bronze) |
| KeyBg | `#CC4A3524` |
| KeyBgHover | `#CC5E4630` |
| KeyBgPressed | `#FF402C1B` |
| KeyBorder | `#1FFFD9A8` (코퍼 하이라이트) |
| HeaderBarHoverBg | `#CC67503A` |
| HeaderBarPressedBg | `#CC79604A` |

### 2.6 목재 변종 (참고 — 별도 재질 슬롯이 아니라 wood 확장 옵션)

현재 `wood`=오크(honey cream / walnut-oak). 같은 슬롯에서 품종 선택으로 대체할 수 있는 후보:

| 품종 | 자연색 (hex) | 성격 | 적용 아이디어 |
|---|---|---|---|
| 메이플 | `#E3C299`/`#E6C89C` | 가장 밝은 목재, 크림-옐로 | 오크 라이트보다 더 밝고 연한 "스칸디나비아" 느낌 |
| 월넛 | `#5B4636`/`#5C4033` | 미디엄 브라운, 범용성 최고 | 오크 다크와 유사도 높음 — 라이트 쪽(크림+월넛) 조합이 더 구분됨 |
| 에보니 | `#211A17` | 쿨한 거의-블랙, 깊이감 | "다크 우드"의 끝 — 탄소/유리 다크와 차별화하려면 따뜻한 브라운 힌트 유지 필요 |

## 3. 제외 후보 (이유)

| 소재 | 제외 이유 |
|---|---|
| 마그네슘 합금 | "Sleek, Minimalist" — 알루미늄과 시각적 차이가 v1 기준(한눈에 구분)에 못 미침 |
| 레진·투명 수지 | 투명/반투명 표현은 플랫 브러시 7개 키로는 불가 (v1 범위 밖) |
| 황동·강철 | 실제 키보드에서는 케이스가 아니라 웨이트/플레이트 용도 — 하우징 재질로 설득력 낮음 |

## 4. 결정 (2026-11-22 — v3, plan.md §3.3 반영)

4개 슬롯(메탈-라이트/메탈-다크/우드-라이트/우드-다크)에 각각 소재를 지정했다.

| 슬롯 | 선정 소재 | 근거 | 탈락 후보 |
|---|---|---|---|
| 메탈-라이트 | **알루미늄** (anodized silver) | v2 값 유지 — 이미 "알루미늄 하우징" 컨셉 | 티타늄(무뎌려서 라이트 느낌 약함), 구리(색채 과함) |
| 메탈-다크 | **티타늄 건메탈** | 무디한 프리미엄 그레이 — 스페이스 그레이(블루)·탄소(순수 중립 블랙)·플라스틱 다크(중립 블랙)와 모두 축이 다름. 실제 하우징 소재 | 탄소섬유(강력한 대안 — "스텔스" 컨셉을 원하면 v4 후보), 구리(색채 과함) |
| 우드-라이트 | **메이플** (pale cream) | 가장 밝은 목재 — 꿀-오크보다 더 연하고 깨끗, 미니멀 브랜드 톤에 맞음 (Lofree 메이플 실존) | 오크(꿀색이 농후), 월넛(라이트 슬롯엔 어둡다) |
| 우드-다크 | **월넛** | "가장 범용성 높은" 미디엄 브라운 — 다크 오크(에스프레소)보다 밝고 붉은 포화, 키 대비 확보 | 에보니(#211A17, 플라스틱 다크와 구분도 부족), 메이플(다크 슬롯엔 밝다) |

**v4 (2026-11-22)**: "유리/세라믹·탄소도 결국 플라스틱 일종"이라는 결정으로 기본 카테고리(플라스틱)에 **라이트=유리/세라믹, 다크=탄소섬유** 마감을 부여 — 별도 카테고리가 아니라 플라스틱의 라이트/다크 변종으로. `LightPlasticMaterial.xaml`·`DarkPlasticMaterial.xaml` 신규, 테스트용이라 기본값 첫 화면이 바뀜(검증 후 유지 결정).

**v6 (2026-11-22)**: **패브릭을 제4 카테고리로 채택** — 라이트=**리넨 에크루**, 다크=**찰콜 직조** (§2.4 값 그대로 + 액센트 브러시 5개). `LightFabricMaterial.xaml`·`DarkFabricMaterial.xaml` 신규. 구리는 오픈 질문 2(색채와 브랜드 톤 궁합) 확인 후 잔존 후보로 남김.
**v7 (2026-11-22)**: 샌드박스 검증에서 패브릭 라이트가 우드(메이플 크림)와 거의 중복 → **구리를 제4 카테고리로 채택**(§2.5 값 + 액센트 브러시 5개), 패브릭 XAML은 삭제. 오픈 질문 2 해소 — 색채는 "과하지 않게" 안료 톤(라이트 샴페인/다크 에스프레소)으로 다듬어 적용.
**v8 (2026-11-22)**: "소재가 아닌 다른 축의 카테고리" 브레인스토밍 결과 이 기능 범위에 맞는 후보 없음 → **제4 카테고리(구리) 제거**. 구리는 §2.5 값 그대로 잔존 후보.
## 5. 오픈 질문

1. ~~v2에 몇 개를 추가할 것인가?~~ → v4에서 유리/세라믹·탄소섬유는 플라스틱 카테고리로 흡수됨. **패브릭이 제4 카테고리로 채택(v6) → 우드와 중복으로 구리로 교체(v7) → v8에서 카테고리 전체 제거.**
2. ~~구리는 색채가 있는 유일한 재질 — 브랜드 톤 궁합?~~ → v8 카테고리 제거로 미결 — 색채 축은 좋으나 이 기능 범위(브러시 오버라이드) 내에선 다른 후보보다 강하지 못해 §2.5 값만 보존.
3. 목재 품종(메이플/월넛/에보니)을 wood 슬롯 내부 옵션으로 갈지, 아예 오크 고정으로 둘지.
4. 각 시작 색값은 plan.md §3.3과 같은 절차로 샌드박스 수동 검증 후 확정.

### 출처 (주요)

- KBDfans — Mechanical Keyboard Materials & Sound Guide
- peripheral-stack — Next-Gen Keyboard Materials: Carbon Fiber, Magnesium & Woods
- mechkeyfoundry — Keyboard Case Material Guide: Aluminum vs Plastic
- mechanicalkeyboard.com.au — Materials, Finishes, and Modding Tricks
- abduzeedo / clrn.org / rainbrand — Teenage Engineering 디자인 분석 (aluminum/ABS/텍스타일, `--te-aluminum #d4d4d4`)
- jcautocarbon / textiletuts / color-name — 탄소섬유 색 (`#262626`~`#2E2E2E`, 트윌 직조)
- kive.ai / favocolor / colourwise — 티타늄 `#878681`, 건메탈 `#2A3439`
- fictiv / refero-design-library — Apple-like aesthetic (frost `#f5f5f7`, carbon `#1d1d1f`, 순백)
- allcottonandlinen / colors.codes — 리넨 `#FAF0E6`/`#F6F1EB`, oatmeal/taupe
- coloried / moda.app / media.io — brushed copper `#C77826`, copper `#B87333`, dark bronze `#804A00`
- usadecoridea / cozy-found — 목재 hex 표 (메이플/월넛/에보니 등)
