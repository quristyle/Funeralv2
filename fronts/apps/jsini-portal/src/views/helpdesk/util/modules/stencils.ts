/**
 * 다이어그램 도형 라이브러리.
 *
 * JinReception 의 `views/pages/utils/stencils.js` 를 그대로 가져왔다.
 * 화살표 / 기본도형 / BPMN / 순서도 네 묶음의 stencil XML 정의이며,
 * 내용은 도형 좌표 데이터라 손대지 않았다(로직 없음).
 */
import { StencilShape, StencilShapeRegistry } from '@maxgraph/core';

/** 팔레트에 표시할 도형 하나 */
export interface StencilShapeInfo {
  h: number;
  label: string;
  registryName: string;
  w: number;
}

/** 도형 묶음 */
export interface StencilGroup {
  name: string;
  prefix: string;
  shapes: StencilShapeInfo[];
  xml: string;
}

const ARROWS_XML = `<shapes name="mxGraph.arrows">
<shape name="Arrow Down" h="97.5" w="70" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
</connections>
<background>
<path>
<move x="20" y="0"/>
<line x="20" y="59"/>
<line x="0" y="59"/>
<line x="35" y="97.5"/>
<line x="70" y="59"/>
<line x="50" y="59"/>
<line x="50" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Arrow Left" h="70" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="97.5" y="20"/>
<line x="38.5" y="20"/>
<line x="38.5" y="0"/>
<line x="0" y="35"/>
<line x="38.5" y="70"/>
<line x="38.5" y="50"/>
<line x="97.5" y="50"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Arrow Right" h="70" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="20"/>
<line x="59" y="20"/>
<line x="59" y="0"/>
<line x="97.5" y="35"/>
<line x="59" y="70"/>
<line x="59" y="50"/>
<line x="0" y="50"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Arrow Up" h="97.5" w="70" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
</connections>
<background>
<path>
<move x="20" y="97.5"/>
<line x="20" y="38.5"/>
<line x="0" y="38.5"/>
<line x="35" y="0"/>
<line x="70" y="38.5"/>
<line x="50" y="38.5"/>
<line x="50" y="97.5"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Bent Left Arrow" h="97" w="97.01" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.85" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.29" perimeter="0" name="W"/>
</connections>
<background>
<path>
<move x="68" y="97"/>
<line x="68" y="48"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="63" y="43"/>
<line x="38" y="43"/>
<line x="38" y="56"/>
<line x="0" y="28"/>
<line x="38" y="0"/>
<line x="38" y="13"/>
<line x="63" y="13"/>
<arc rx="35" ry="35" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="97" y="48"/>
<line x="97" y="97"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Bent Right Arrow" h="97" w="97.01" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.15" y="1" perimeter="0" name="S"/>
<constraint x="1" y="0.29" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="29.01" y="97"/>
<line x="29.01" y="48"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="34.01" y="43"/>
<line x="59.01" y="43"/>
<line x="59.01" y="56"/>
<line x="97.01" y="28"/>
<line x="59.01" y="0"/>
<line x="59.01" y="13"/>
<line x="34.01" y="13"/>
<arc rx="35" ry="35" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="0.01" y="48"/>
<line x="0.01" y="97"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Bent Up Arrow" h="83.5" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.71" y="0" perimeter="0" name="N"/>
<constraint x="0" y="0.82" perimeter="0" name="W"/>
</connections>
<background>
<path>
<move x="0" y="53.5"/>
<line x="54" y="53.5"/>
<line x="54" y="23.5"/>
<line x="42" y="23.5"/>
<line x="69" y="0"/>
<line x="97" y="23.5"/>
<line x="84" y="23.5"/>
<line x="84" y="83.5"/>
<line x="0" y="83.5"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Callout Double Arrow" h="97.5" w="50" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
</connections>
<background>
<path>
<move x="15" y="24"/>
<line x="15" y="19"/>
<line x="6" y="19"/>
<line x="25" y="0"/>
<line x="44" y="19"/>
<line x="35" y="19"/>
<line x="35" y="24"/>
<line x="50" y="24"/>
<line x="50" y="74"/>
<line x="35" y="74"/>
<line x="35" y="79"/>
<line x="44" y="79"/>
<line x="25" y="97.5"/>
<line x="6" y="79"/>
<line x="15" y="79"/>
<line x="15" y="74"/>
<line x="0" y="74"/>
<line x="0" y="24"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Callout Quad Arrow" h="97" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="38.5" y="23.5"/>
<line x="38.5" y="18.5"/>
<line x="29.5" y="18.5"/>
<line x="48.5" y="0"/>
<line x="67.5" y="18.5"/>
<line x="58.5" y="18.5"/>
<line x="58.5" y="23.5"/>
<line x="73.5" y="23.5"/>
<line x="73.5" y="38.5"/>
<line x="78.5" y="38.5"/>
<line x="78.5" y="29.5"/>
<line x="97" y="48.5"/>
<line x="78.5" y="67.5"/>
<line x="78.5" y="58.5"/>
<line x="73.5" y="58.5"/>
<line x="73.5" y="73.5"/>
<line x="58.5" y="73.5"/>
<line x="58.5" y="78.5"/>
<line x="67.5" y="78.5"/>
<line x="48.5" y="97"/>
<line x="29.5" y="78.5"/>
<line x="38.5" y="78.5"/>
<line x="38.5" y="73.5"/>
<line x="23.5" y="73.5"/>
<line x="23.5" y="58.5"/>
<line x="18.5" y="58.5"/>
<line x="18.5" y="67.5"/>
<line x="0" y="48.5"/>
<line x="18.5" y="29.5"/>
<line x="18.5" y="38.5"/>
<line x="23.5" y="38.5"/>
<line x="23.5" y="23.5"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Callout Up Arrow" h="98" w="60" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
</connections>
<background>
<path>
<move x="20" y="39"/>
<line x="20" y="19"/>
<line x="11" y="19"/>
<line x="30" y="0"/>
<line x="49" y="19"/>
<line x="40" y="19"/>
<line x="40" y="39"/>
<line x="60" y="39"/>
<line x="60" y="98"/>
<line x="0" y="98"/>
<line x="0" y="39"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Chevron Arrow" h="60" w="96" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.31" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="30" y="30"/>
<line x="0" y="0"/>
<line x="66" y="0"/>
<line x="96" y="30"/>
<line x="66" y="60"/>
<line x="0" y="60"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Circular Arrow" h="69.5" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.12" y="0.64" perimeter="0" name="SW"/>
<constraint x="0.794" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="44.5"/>
<arc rx="44.5" ry="44.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="89" y="44.5"/>
<line x="97" y="44.5"/>
<line x="77" y="69.5"/>
<line x="57" y="44.5"/>
<line x="65" y="44.5"/>
<arc rx="20.5" ry="20.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="24" y="44.5"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Jump-in Arrow 1" h="99.41" w="96" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.024" perimeter="0" name="NW"/>
<constraint x="0.657" y="1" perimeter="0" name="S"/>
</connections>
<background>

<linejoin join="round"/>
<path>
<move x="30" y="60.41"/>
<line x="48" y="60.41"/>
<arc rx="60" ry="60" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="0" y="2.41"/>
<arc rx="75" ry="75" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="78" y="60.41"/>
<line x="96" y="60.41"/>
<line x="63" y="99.41"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Jump-in Arrow 2" h="99.41" w="96" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="1" y="0.024" perimeter="0" name="NE"/>
<constraint x="0.343" y="1" perimeter="0" name="S"/>
</connections>
<background>

<linejoin join="round"/>
<path>
<move x="66" y="60.41"/>
<line x="48" y="60.41"/>
<arc rx="60" ry="60" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="96" y="2.41"/>
<arc rx="75" ry="75" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="18" y="60.41"/>
<line x="0" y="60.41"/>
<line x="33" y="99.41"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Left and Up Arrow" h="96.5" w="96.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.71" perimeter="0" name="W"/>
<constraint x="0.71" y="0" perimeter="0" name="N"/>
</connections>
<background>
<path>
<move x="23.5" y="53.5"/>
<line x="53.5" y="53.5"/>
<line x="53.5" y="23.5"/>
<line x="41.5" y="23.5"/>
<line x="68.5" y="0"/>
<line x="96.5" y="23.5"/>
<line x="83.5" y="23.5"/>
<line x="83.5" y="83.5"/>
<line x="23.5" y="83.5"/>
<line x="23.5" y="96.5"/>
<line x="0" y="68.5"/>
<line x="23.5" y="41.5"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Left Sharp Edged Head Arrow" h="60" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="97.5" y="20"/>
<line x="18.5" y="20"/>
<line x="30.5" y="0"/>
<line x="18.5" y="0"/>
<line x="0" y="30"/>
<line x="18.5" y="60"/>
<line x="30.5" y="60"/>
<line x="18.5" y="40"/>
<line x="97.5" y="40"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Notched Signal-in Arrow" h="30" w="96.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.13" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="0"/>
<line x="83" y="0"/>
<line x="96.5" y="15"/>
<line x="83" y="30"/>
<line x="0" y="30"/>
<line x="13" y="15"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Quad Arrow" h="97.5" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="39" y="39"/>
<line x="39" y="19"/>
<line x="30" y="19"/>
<line x="49" y="0"/>
<line x="68" y="19"/>
<line x="59" y="19"/>
<line x="59" y="39"/>
<line x="79" y="39"/>
<line x="79" y="30"/>
<line x="97.5" y="49"/>
<line x="79" y="68"/>
<line x="79" y="59"/>
<line x="59" y="59"/>
<line x="59" y="79"/>
<line x="68" y="79"/>
<line x="49" y="97.5"/>
<line x="30" y="79"/>
<line x="39" y="79"/>
<line x="39" y="59"/>
<line x="19" y="59"/>
<line x="19" y="68"/>
<line x="0" y="49"/>
<line x="19" y="30"/>
<line x="19" y="39"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Right Notched Arrow" h="70" w="96.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.13" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="20"/>
<line x="58" y="20"/>
<line x="58" y="0"/>
<line x="96.5" y="35"/>
<line x="58" y="70"/>
<line x="58" y="50"/>
<line x="0" y="50"/>
<line x="13" y="35"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Sharp Edged Arrow" h="60" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="97.5" y="20"/>
<line x="18.5" y="20"/>
<line x="27.5" y="5"/>
<line x="18.5" y="0"/>
<line x="0" y="30"/>
<line x="18.5" y="60"/>
<line x="27.5" y="55"/>
<line x="18.5" y="40"/>
<line x="97.5" y="40"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Signal-in Arrow" h="30" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="0"/>
<line x="84" y="0"/>
<line x="97.5" y="15"/>
<line x="84" y="30"/>
<line x="0" y="30"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Slender Left Arrow" h="60" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="97.5" y="20"/>
<line x="18.5" y="20"/>
<line x="18.5" y="0"/>
<line x="0" y="30"/>
<line x="18.5" y="60"/>
<line x="18.5" y="40"/>
<line x="97.5" y="40"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Slender Two Way Arrow" h="60" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="78.5" y="20"/>
<line x="18.5" y="20"/>
<line x="18.5" y="0"/>
<line x="0" y="30"/>
<line x="18.5" y="60"/>
<line x="18.5" y="40"/>
<line x="78.5" y="40"/>
<line x="78.5" y="60"/>
<line x="97.5" y="30"/>
<line x="78.5" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Slender Wide Tailed Arrow" h="60" w="96.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="0.8" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="58.5" y="20"/>
<line x="18.5" y="20"/>
<line x="18.5" y="0"/>
<line x="0" y="30"/>
<line x="18.5" y="60"/>
<line x="18.5" y="40"/>
<line x="58.5" y="40"/>
<line x="73.5" y="60"/>
<line x="96.5" y="60"/>
<line x="76.5" y="30"/>
<line x="96.5" y="0"/>
<line x="73.5" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Striped Arrow" h="70" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="24" y="20"/>
<line x="59" y="20"/>
<line x="59" y="0"/>
<line x="97.5" y="35"/>
<line x="59" y="70"/>
<line x="59" y="50"/>
<line x="24" y="50"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<rect x="8" y="20" w="12" h="30"/>
<fillstroke/>
<rect x="0" y="20" w="4" h="30"/>
<fillstroke/>
</foreground>
</shape>
<shape name="Stylised Notched Arrow" h="60" w="96.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.13" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>

<miterlimit limit="8"/>
<path>
<move x="0" y="5"/>
<line x="68" y="20"/>
<line x="58" y="0"/>
<line x="96.5" y="30"/>
<line x="58" y="60"/>
<line x="68" y="45"/>
<line x="0" y="55"/>
<line x="13" y="30"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Triad Arrow" h="68" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.72" perimeter="0" name="W"/>
<constraint x="1" y="0.72" perimeter="0" name="E"/>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
</connections>
<background>
<path>
<move x="39" y="39"/>
<line x="39" y="19"/>
<line x="30" y="19"/>
<line x="49" y="0"/>
<line x="68" y="19"/>
<line x="59" y="19"/>
<line x="59" y="39"/>
<line x="79" y="39"/>
<line x="79" y="30"/>
<line x="97.5" y="49"/>
<line x="79" y="68"/>
<line x="79" y="59"/>
<line x="39" y="59"/>
<line x="19" y="59"/>
<line x="19" y="68"/>
<line x="0" y="49"/>
<line x="19" y="30"/>
<line x="19" y="39"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Two Way Arrow Horizontal" h="60" w="96" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="63" y="15"/>
<line x="63" y="0"/>
<line x="96" y="30"/>
<line x="63" y="60"/>
<line x="63" y="45"/>
<line x="33" y="45"/>
<line x="33" y="60"/>
<line x="0" y="30"/>
<line x="33" y="0"/>
<line x="33" y="15"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Two Way Arrow Vertical" h="96" w="60" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
</connections>
<background>
<path>
<move x="15" y="63"/>
<line x="0" y="63"/>
<line x="30" y="96"/>
<line x="60" y="63"/>
<line x="45" y="63"/>
<line x="45" y="33"/>
<line x="60" y="33"/>
<line x="30" y="0"/>
<line x="0" y="33"/>
<line x="15" y="33"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="U Turn Arrow" h="98" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.12" y="1" perimeter="0" name="SW"/>
<constraint x="0.792" y="0.71" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="44.5"/>
<arc rx="44.5" ry="44.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="89" y="44.5"/>
<line x="97" y="44.5"/>
<line x="77" y="69.5"/>
<line x="57" y="44.5"/>
<line x="65" y="44.5"/>
<arc rx="20.5" ry="20.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="24" y="44.83"/>
<line x="24" y="98"/>
<line x="0" y="98"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="U Turn Down Arrow" h="62" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.91" y="1" perimeter="0" name="SE"/>
<constraint x="0.237" y="1" perimeter="0" name="SW"/>
</connections>
<background>
<path>
<move x="97" y="62"/>
<line x="97" y="32"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="33" y="32"/>
<line x="46" y="32"/>
<line x="23" y="62"/>
<line x="0" y="32"/>
<line x="13" y="32"/>
<arc rx="32" ry="32" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="45" y="0"/>
<line x="65" y="0"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="53" y="3"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="78" y="32"/>
<line x="78" y="62"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="U Turn Left Arrow" h="97.07" w="62.23" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.76" perimeter="0" name="SW"/>
<constraint x="0" y="0.1" perimeter="0" name="NW"/>
</connections>
<background>
<path>
<move x="0" y="0.19"/>
<line x="30" y="0.07"/>
<arc rx="30" ry="30" x-axis-rotation="-90.22" large-arc-flag="0" sweep-flag="1" x="30.25" y="64.07"/>
<line x="30.2" y="51.07"/>
<line x="0.29" y="74.19"/>
<line x="30.37" y="97.07"/>
<line x="30.32" y="84.07"/>
<arc rx="32" ry="32" x-axis-rotation="-90.22" large-arc-flag="0" sweep-flag="0" x="62.2" y="51.95"/>
<line x="62.13" y="31.95"/>
<arc rx="30" ry="30" x-axis-rotation="-90.22" large-arc-flag="0" sweep-flag="1" x="59.17" y="43.96"/>
<arc rx="30" ry="30" x-axis-rotation="-90.22" large-arc-flag="0" sweep-flag="0" x="30.08" y="19.07"/>
<line x="0.08" y="19.19"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="U Turn Right Arrow" h="97.07" w="62.23" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="1" y="0.76" perimeter="0" name="SW"/>
<constraint x="1" y="0.1" perimeter="0" name="NW"/>
</connections>
<background>
<path>
<move x="62.23" y="0.19"/>
<line x="32.23" y="0.07"/>
<arc rx="30" ry="30" x-axis-rotation="-89.78" large-arc-flag="0" sweep-flag="0" x="31.99" y="64.07"/>
<line x="32.03" y="51.07"/>
<line x="61.95" y="74.19"/>
<line x="31.86" y="97.07"/>
<line x="31.91" y="84.07"/>
<arc rx="32" ry="32" x-axis-rotation="-89.78" large-arc-flag="1" sweep-flag="1" x="0.03" y="51.95"/>
<line x="0.11" y="31.95"/>
<arc rx="30" ry="30" x-axis-rotation="-89.78" large-arc-flag="0" sweep-flag="0" x="3.06" y="43.96"/>
<arc rx="30" ry="30" x-axis-rotation="-89.78" large-arc-flag="1" sweep-flag="1" x="32.16" y="19.07"/>
<line x="62.16" y="19.19"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="U Turn Up Arrow" h="62" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.91" y="0" perimeter="0" name="NE"/>
<constraint x="0.237" y="0" perimeter="0" name="NW"/>
</connections>
<background>
<path>
<move x="97" y="0"/>
<line x="97" y="30"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="33" y="30"/>
<line x="46" y="30"/>
<line x="23" y="0"/>
<line x="0" y="30"/>
<line x="13" y="30"/>
<arc rx="32" ry="32" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="45" y="62"/>
<line x="65" y="62"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="53" y="59"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="78" y="30"/>
<line x="78" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
</shapes>`;

const BASIC_XML = `<shapes name="mxgraph.basic">
<shape name="4 Point Star" h="92" w="92" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="46" y="0"/>
<line x="56" y="36"/>
<line x="92" y="46"/>
<line x="56" y="56"/>
<line x="46" y="92"/>
<line x="36" y="56"/>
<line x="0" y="46"/>
<line x="36" y="36"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="6 Point Star" h="84.5" w="96" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.24" y="0" perimeter="0" name="N1"/>
<constraint x="0.24" y="1" perimeter="0" name="S1"/>
<constraint x="0.76" y="0" perimeter="0" name="N2"/>
<constraint x="0.76" y="1" perimeter="0" name="S2"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="23" y="28.9"/>
<line x="23" y="0"/>
<line x="48" y="14.4"/>
<line x="73" y="0"/>
<line x="73" y="28.9"/>
<line x="96" y="42.2"/>
<line x="73" y="55.6"/>
<line x="73" y="84.5"/>
<line x="48" y="70"/>
<line x="23" y="84.5"/>
<line x="23" y="55.6"/>
<line x="0" y="42.2"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="8 Point Star" h="96" w="96" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.29" y="0" perimeter="0" name="N1"/>
<constraint x="0.29" y="1" perimeter="0" name="S1"/>
<constraint x="0.71" y="0" perimeter="0" name="N2"/>
<constraint x="0.71" y="1" perimeter="0" name="S2"/>
<constraint x="0" y="0.29" perimeter="0" name="W1"/>
<constraint x="0" y="0.71" perimeter="0" name="W2"/>
<constraint x="1" y="0.29" perimeter="0" name="E1"/>
<constraint x="1" y="0.71" perimeter="0" name="E2"/>
</connections>
<background>
<path>
<move x="28" y="28"/>
<line x="28" y="0"/>
<line x="48" y="20"/>
<line x="68" y="0"/>
<line x="68" y="28"/>
<line x="96" y="28"/>
<line x="76" y="48"/>
<line x="96" y="68"/>
<line x="68" y="68"/>
<line x="68" y="96"/>
<line x="48" y="76"/>
<line x="28" y="96"/>
<line x="28" y="68"/>
<line x="0" y="68"/>
<line x="20" y="48"/>
<line x="0" y="28"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Banner" h="50" w="96" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="0.8" perimeter="0" name="S"/>
<constraint x="0.13" y="0.6" perimeter="0" name="W"/>
<constraint x="0.87" y="0.6" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="50"/>
<line x="38" y="50"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="40.5" y="47.5"/>
<line x="40.5" y="40"/>
<line x="55.5" y="40"/>
<line x="55.5" y="47.5"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="58" y="50"/>
<line x="96" y="50"/>
<line x="83" y="30"/>
<line x="96" y="10"/>
<line x="70.5" y="10"/>
<line x="70.5" y="2.5"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="68" y="0"/>
<line x="28" y="0"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="25.5" y="2.5"/>
<line x="25.5" y="10"/>
<line x="0" y="10"/>
<line x="13" y="30"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<path>
<move x="40.5" y="47.5"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="38" y="45"/>
<line x="28" y="45"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="28" y="40"/>
<line x="68" y="40"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="68" y="45"/>
<line x="58" y="45"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="55.5" y="47.5"/>
<move x="25.5" y="42.5"/>
<line x="25.5" y="10"/>
<move x="70.5" y="42.5"/>
<line x="70.5" y="10"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Cloud Callout" h="61.4" w="90.41" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="0.74" perimeter="0" name="S"/>
<constraint x="0.015" y="0.4" perimeter="0" name="W"/>
<constraint x="0.993" y="0.4" perimeter="0" name="E"/>
<constraint x="0.01" y="0.995" perimeter="0" name="SW"/>
</connections>
<background>
<save/>
<linejoin join="round"/>
<path>
<move x="12.1" y="31.8"/>
<arc rx="8" ry="8" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="12.1" y="16.8"/>
<arc rx="12" ry="12" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="33.1" y="8.8"/>
<arc rx="14" ry="14" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="59.1" y="8.8"/>
<arc rx="12" ry="12" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="79.1" y="16.8"/>
<arc rx="8" ry="8" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="79.1" y="31.8"/>
<arc rx="12" ry="12" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="58.1" y="38.8"/>
<arc rx="14" ry="14" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="34.1" y="38.8"/>
<arc rx="10" ry="8" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="12.1" y="31.8"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<restore/>
<linejoin join="miter"/>
<ellipse x="9.1" y="46.1" w="12" h="5.4"/>
<fillstroke/>
<ellipse x="4.3" y="53.5" w="7.6" h="3.6"/>
<fillstroke/>
<ellipse x="0" y="58.8" w="4.8" h="2.6"/>
<fillstroke/>
</foreground>
</shape>
<shape name="Cone" h="96.91" w="99" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
</connections>
<background>
<path>
<move x="49.5" y="0"/>
<line x="99" y="88"/>
<arc rx="25" ry="4.5" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="0" y="88"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<path>
<move x="0" y="88"/>
<arc rx="25" ry="4.5" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="99" y="88"/>
</path>
<fillstroke/>
</foreground>
</shape>
<shape name="Cross" h="98" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="34"/>
<line x="34" y="34"/>
<line x="34" y="0"/>
<line x="64" y="0"/>
<line x="64" y="34"/>
<line x="98" y="34"/>
<line x="98" y="64"/>
<line x="64" y="64"/>
<line x="64" y="98"/>
<line x="34" y="98"/>
<line x="34" y="64"/>
<line x="0" y="64"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Document" h="98" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="98" y="14"/>
<line x="98" y="98"/>
<line x="0" y="98"/>
<line x="0" y="0"/>
<line x="84" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<path>
<move x="84" y="0"/>
<arc rx="18" ry="10" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="79" y="9"/>
<line x="98" y="14"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Flash" h="95.5" w="60" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.565" y="0" perimeter="0" name="N"/>
<constraint x="0" y="0.995" perimeter="0" name="SW"/>
</connections>
<background>

<miterlimit limit="6"/>
<path>
<move x="0" y="95.5"/>
<line x="20" y="75.5"/>
<line x="3" y="61.5"/>
<line x="20" y="49.5"/>
<line x="3" y="31.5"/>
<line x="34" y="0"/>
<line x="60" y="25.5"/>
<line x="36" y="39.5"/>
<line x="50" y="53.5"/>
<line x="29" y="65.5"/>
<line x="42" y="76.5"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Half Circle" h="49" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
</connections>
<background>
<path>
<move x="0" y="0"/>
<arc rx="44.5" ry="44.5" x-axis-rotation="0" large-arc-flag="1" sweep-flag="0" x="98" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Heart" h="94.74" w="103.89" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0.115" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0.07" y="0.5" perimeter="0" name="W"/>
<constraint x="0.93" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="51.94" y="94.74"/>
<curve x1="55.79" y1="90.78" x2="77.8" y2="68.16" x3="91.56" y3="54.03"/>
<curve x1="103.89" y1="41.37" x2="103.62" y2="22.91" x3="92.42" y3="11.46"/>
<curve x1="81.21" y1="0" x2="63.09" y2="0.05" x3="51.94" y3="11.56"/>
<curve x1="40.79" y1="0.05" x2="22.67" y2="0" x3="11.47" y3="11.45"/>
<curve x1="0.26" y1="22.9" x2="0" y2="41.36" x3="12.32" y3="54.03"/>
<curve x1="26.08" y1="68.16" x2="48.09" y2="90.78" x3="51.94" y3="94.74"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Loud Callout" h="59.9" w="93.3" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.49" y="0" perimeter="0" name="N"/>
<constraint x="0.52" y="0.91" perimeter="0" name="S"/>
<constraint x="0" y="0.51" perimeter="0" name="W"/>
<constraint x="0.99" y="0.503" perimeter="0" name="E"/>
<constraint x="0.04" y="1" perimeter="0" name="SE"/>
</connections>
<background>

<miterlimit limit="10"/>
<path>
<move x="14.9" y="43.9"/>
<line x="9.3" y="46.7"/>
<line x="11.1" y="40.9"/>
<line x="6.6" y="43.9"/>
<line x="8.3" y="39.2"/>
<line x="2.8" y="40.8"/>
<line x="6.6" y="36.4"/>
<line x="0.9" y="36.2"/>
<line x="5.8" y="32.7"/>
<line x="0" y="30.8"/>
<line x="5.3" y="28.2"/>
<line x="0.3" y="25.6"/>
<line x="5.9" y="24.19"/>
<line x="0.8" y="19.9"/>
<line x="6.5" y="19.8"/>
<line x="2.8" y="15.1"/>
<line x="8.2" y="16.1"/>
<line x="5.9" y="11.3"/>
<line x="11.5" y="13.2"/>
<line x="10.2" y="8.7"/>
<line x="15.7" y="10.6"/>
<line x="14.9" y="6.15"/>
<line x="19.2" y="9.3"/>
<line x="19.8" y="4.3"/>
<line x="23.4" y="8"/>
<line x="23.8" y="3.4"/>
<line x="28.5" y="6.9"/>
<line x="30.3" y="1.3"/>
<line x="33.3" y="6.2"/>
<line x="34.7" y="0.6"/>
<line x="38.2" y="6"/>
<line x="40.6" y="0"/>
<line x="42.8" y="5.8"/>
<line x="45.6" y="0"/>
<line x="47.1" y="6"/>
<line x="51.3" y="1"/>
<line x="50.8" y="6.3"/>
<line x="55.4" y="0.6"/>
<line x="55.1" y="6.6"/>
<line x="60.5" y="1.4"/>
<line x="61.1" y="7.1"/>
<line x="66.1" y="2.7"/>
<line x="66.2" y="8.7"/>
<line x="71.9" y="4.4"/>
<line x="70.5" y="10"/>
<line x="77.6" y="6.2"/>
<line x="74.9" y="11.8"/>
<line x="83.9" y="7.8"/>
<line x="80.1" y="13.6"/>
<line x="88.1" y="11.9"/>
<line x="85.2" y="17"/>
<line x="91.2" y="16.9"/>
<line x="87" y="20.1"/>
<line x="93.3" y="21.2"/>
<line x="87.9" y="24"/>
<line x="93.2" y="25.8"/>
<line x="86.8" y="26.8"/>
<line x="92.4" y="30.3"/>
<line x="86.6" y="30.8"/>
<line x="90.9" y="34.8"/>
<line x="84.2" y="33.5"/>
<line x="87.8" y="38.8"/>
<line x="82" y="36.6"/>
<line x="84.7" y="41.7"/>
<line x="79.2" y="40.7"/>
<line x="79.8" y="46"/>
<line x="76.3" y="42.9"/>
<line x="75.6" y="48.6"/>
<line x="72" y="44.7"/>
<line x="71.7" y="51.2"/>
<line x="68" y="46"/>
<line x="66.2" y="52.1"/>
<line x="63.7" y="46.6"/>
<line x="61.2" y="53.7"/>
<line x="59.7" y="47.6"/>
<line x="56.9" y="53.8"/>
<line x="55" y="48.1"/>
<line x="52.8" y="53.9"/>
<line x="50.9" y="48.1"/>
<line x="48.4" y="54.5"/>
<line x="47" y="48.1"/>
<line x="44.4" y="53.7"/>
<line x="43.2" y="47.4"/>
<line x="40.1" y="54.2"/>
<line x="38.8" y="47.4"/>
<line x="36.3" y="54.7"/>
<line x="35.6" y="47.8"/>
<line x="32.4" y="55.1"/>
<line x="30.9" y="46.6"/>
<line x="28.6" y="53.3"/>
<line x="26.8" y="47.8"/>
<line x="3.8" y="59.9"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Moon" h="103.05" w="77.05" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.48" y="0" perimeter="0" name="N"/>
<constraint x="1" y="0.89" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="37.05" y="0"/>
<arc rx="48" ry="48" x-axis-rotation="0" large-arc-flag="1" sweep-flag="0" x="77.05" y="92"/>
<arc rx="60" ry="60" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="37.05" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="No Symbol" h="100" w="100" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="0.855" y="0.855" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="50"/>
<arc rx="35" ry="35" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="100" y="50"/>
<arc rx="35" ry="35" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="50"/>
<close/>
<move x="78.95" y="69.7"/>
<arc rx="35" ry="35" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="30.3" y="21.05"/>
<close/>
<move x="21.15" y="30.3"/>
<arc rx="35" ry="35" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="69.7" y="79"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Octagon" h="98" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="29"/>
<line x="29" y="0"/>
<line x="69" y="0"/>
<line x="98" y="29"/>
<line x="98" y="69"/>
<line x="69" y="98"/>
<line x="29" y="98"/>
<line x="0" y="69"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Orthogonal Triangle" h="97" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0" perimeter="0" name="NW"/>
<constraint x="0" y="1" perimeter="0" name="SW"/>
<constraint x="1" y="1" perimeter="0" name="SE"/>
<constraint x="0.5" y="0.5" perimeter="0" name="center"/>
</connections>
<background>
<path>
<move x="0" y="97"/>
<line x="0" y="0"/>
<line x="97" y="97"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Oval Callout" h="63.15" w="109.43" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0.045" perimeter="0" name="N"/>
<constraint x="0.5" y="0.84" perimeter="0" name="S"/>
<constraint x="0.045" y="0.45" perimeter="0" name="W"/>
<constraint x="0.945" y="0.45" perimeter="0" name="E"/>
<constraint x="0.08" y="1" perimeter="0" name="SW"/>
</connections>
<background>

<miterlimit limit="15"/>
<path>
<move x="20.53" y="46.15"/>
<arc rx="49" ry="25" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="31.53" y="50.15"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="9.03" y="63.15"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="20.53" y="46.15"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Parallelepiped" h="60" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0.12" y="0.5" perimeter="0" name="W"/>
<constraint x="0.88" y="0.5" perimeter="0" name="E"/>
<constraint x="0.24" y="0" perimeter="0" name="NW"/>
<constraint x="1" y="0" perimeter="0" name="NE"/>
<constraint x="0.76" y="1" perimeter="0" name="SE"/>
<constraint x="0" y="1" perimeter="0" name="SW"/>
</connections>
<background>
<path>
<move x="0" y="60"/>
<line x="23.5" y="0"/>
<line x="97" y="0"/>
<line x="73.5" y="60"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Pentagon" h="90" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0" y="0.365" perimeter="0" name="W"/>
<constraint x="1" y="0.365" perimeter="0" name="E"/>
<constraint x="0.81" y="1" perimeter="0" name="SE"/>
<constraint x="0.19" y="1" perimeter="0" name="SW"/>
</connections>
<background>
<path>
<move x="18.5" y="90"/>
<line x="0" y="33"/>
<line x="48.5" y="0"/>
<line x="97" y="33"/>
<line x="78.5" y="90"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Rectangular Callout" h="60" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="0.715" perimeter="0" name="S"/>
<constraint x="0" y="0.355" perimeter="0" name="W"/>
<constraint x="1" y="0.355" perimeter="0" name="E"/>
<constraint x="0.04" y="1" perimeter="0" name="SW"/>
</connections>
<background>

<miterlimit limit="10"/>
<path>
<move x="15" y="43"/>
<line x="0" y="43"/>
<line x="0" y="0"/>
<line x="98" y="0"/>
<line x="98" y="43"/>
<line x="29" y="43"/>
<line x="4" y="60"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Rounded Rectangular Callout" h="60" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="0.715" perimeter="0" name="S"/>
<constraint x="0" y="0.355" perimeter="0" name="W"/>
<constraint x="1" y="0.355" perimeter="0" name="E"/>
<constraint x="0.04" y="1" perimeter="0" name="SW"/>
</connections>
<background>

<miterlimit limit="15"/>
<path>
<move x="15.5" y="43"/>
<line x="5" y="43"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="38"/>
<line x="0" y="5"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="5" y="0"/>
<line x="93" y="0"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="98" y="5"/>
<line x="98" y="38"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="93" y="43"/>
<line x="29" y="43"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="4" y="60"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="15.5" y="43"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Smiley" h="98" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="0.855" y="0.855" perimeter="0" name="SE"/>
</connections>
<background>
<ellipse x="0" y="0" w="98" h="98"/>
</background>
<foreground>
<fillstroke/>
<save/>
<path>
<move x="11" y="54"/>
<arc rx="38" ry="27" x-axis-rotation="0" large-arc-flag="1" sweep-flag="0" x="87" y="54"/>
</path>
<stroke/>
<restore/>
<strokewidth width="1"/>
<path>
<move x="16" y="51"/>
<line x="6" y="57"/>
</path>
<stroke/>
<path>
<move x="82" y="51"/>
<line x="92" y="57"/>
</path>
<stroke/>
<strokewidth width="6"/>
<ellipse x="24" y="27" w="6" h="16"/>
<fillstroke/>
<ellipse x="68" y="27" w="6" h="16"/>
<fillstroke/>
</foreground>
</shape>
<shape name="Star" h="90" w="95" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="0.76" perimeter="0" name="S"/>
<constraint x="0" y="0.367" perimeter="0" name="W"/>
<constraint x="1" y="0.367" perimeter="0" name="E"/>
<constraint x="0.185" y="1" perimeter="0" name="SW"/>
<constraint x="0.815" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="33"/>
<line x="36.4" y="33"/>
<line x="47.5" y="0"/>
<line x="58.6" y="33"/>
<line x="95" y="33"/>
<line x="66" y="55.1"/>
<line x="77.5" y="90"/>
<line x="47.5" y="68.4"/>
<line x="17.5" y="90"/>
<line x="29" y="55.1"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Sun" h="95" w="95" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="0.855" y="0.855" perimeter="0" name="SE"/>
</connections>
<background>
<ellipse x="17.5" y="17.5" w="60" h="60"/>
</background>
<foreground>
<fillstroke/>
<path>
<move x="42.5" y="14.5"/>
<line x="47.5" y="0"/>
<line x="52.5" y="14.5"/>
<close/>
</path>
<fillstroke/>
<path>
<move x="42.5" y="80.5"/>
<line x="47.5" y="95"/>
<line x="52.5" y="80.5"/>
<close/>
</path>
<fillstroke/>
<path>
<move x="14.5" y="42.5"/>
<line x="0" y="47.5"/>
<line x="14.5" y="52.5"/>
<close/>
</path>
<fillstroke/>
<path>
<move x="80.5" y="42.5"/>
<line x="95" y="47.5"/>
<line x="80.5" y="52.5"/>
<close/>
</path>
<fillstroke/>
<path>
<move x="67.5" y="20.5"/>
<line x="81.2" y="13.9"/>
<line x="74.5" y="27.5"/>
<close/>
</path>
<fillstroke/>
<path>
<move x="67.5" y="74.5"/>
<line x="81.2" y="81.1"/>
<line x="74.5" y="67.5"/>
<close/>
</path>
<fillstroke/>
<path>
<move x="27.5" y="20.5"/>
<line x="13.8" y="13.9"/>
<line x="20.5" y="27.5"/>
<close/>
</path>
<fillstroke/>
<path>
<move x="27.5" y="74.5"/>
<line x="13.8" y="81.1"/>
<line x="20.5" y="67.5"/>
<close/>
</path>
<fillstroke/>
</foreground>
</shape>
<shape name="Tick" h="97.54" w="84.4" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.9" y="0.01" perimeter="0" name="N"/>
<constraint x="0.32" y="0.992" perimeter="0" name="S"/>
<constraint x="0" y="0.7" perimeter="0" name="W"/>
<constraint x="1" y="0.06" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0.36" y="66.69"/>
<arc rx="12" ry="12" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="16.36" y="58.69"/>
<arc rx="20" ry="20" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="26.36" y="69.69"/>
<arc rx="200" ry="200" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="63.36" y="5.69"/>
<arc rx="18" ry="18" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="80.36" y="1.69"/>
<arc rx="4.5" ry="4.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="83.36" y="8.69"/>
<arc rx="230" ry="230" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="35.36" y="94.69"/>
<arc rx="20" ry="20" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="17.36" y="94.69"/>
<arc rx="100" ry="100" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="0.36" y="68.69"/>
<arc rx="2" ry="2" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0.36" y="66.69"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Trapezoid" h="98" w="97" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0.12" y="0.5" perimeter="0" name="W"/>
<constraint x="0.88" y="0.5" perimeter="0" name="E"/>
<constraint x="0.24" y="0" perimeter="0" name="NW"/>
<constraint x="0" y="1" perimeter="0" name="SW"/>
<constraint x="0.76" y="0" perimeter="0" name="NE"/>
<constraint x="1" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="98"/>
<line x="23.5" y="0"/>
<line x="73.5" y="0"/>
<line x="97" y="98"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Wave" h="56.7" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0.295" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="8.7"/>
<arc rx="20" ry="20" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="33" y="8.7"/>
<arc rx="20" ry="20" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="65" y="8.7"/>
<arc rx="20" ry="20" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="98" y="8.7"/>
<line x="98" y="48.7"/>
<arc rx="20" ry="20" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="65" y="48.7"/>
<arc rx="20" ry="20" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="33" y="48.7"/>
<arc rx="20" ry="20" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="0" y="48.7"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="X" h="98" w="96" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0.29" perimeter="0" name="N"/>
<constraint x="0.5" y="0.71" perimeter="0" name="S"/>
<constraint x="0.33" y="0.5" perimeter="0" name="W"/>
<constraint x="0.65" y="0.5" perimeter="0" name="E"/>
<constraint x="0" y="0" perimeter="0" name="NW"/>
<constraint x="0" y="1" perimeter="0" name="SW"/>
<constraint x="1" y="0" perimeter="0" name="NE"/>
<constraint x="1" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="0"/>
<line x="28" y="0"/>
<line x="48" y="29"/>
<line x="68" y="0"/>
<line x="96" y="0"/>
<line x="62" y="49"/>
<line x="96" y="98"/>
<line x="68" y="98"/>
<line x="48" y="69"/>
<line x="28" y="98"/>
<line x="0" y="98"/>
<line x="32" y="49"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
</shapes>`;

const BPMN_XML = `<shapes name="mxgraph.bpmn">
<shape h="10.39" name="Ad Hoc" strokewidth="inherit" w="15">
    <connections/>
    <background>
        <path>
            <move x="0" y="1.69"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="1" x="7.5" x-axis-rotation="0" y="1.69"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="0" x="15" x-axis-rotation="0" y="1.69"/>
            <line x="15" y="8.69"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="1" x="7.5" x-axis-rotation="0" y="8.69"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="0" x="0" x-axis-rotation="0" y="8.69"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
    </foreground>
</shape>
<shape h="65" name="Business Rule Task" strokewidth="inherit" w="100">
    <connections/>
    <background>
        <rect h="65" w="100" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <path>
            <move x="0" y="15"/>
            <line x="100" y="15"/>
            <move x="1" y="40"/>
            <line x="99.4" y="40"/>
            <move x="25" y="15"/>
            <line x="25" y="65"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="97" name="Cancel End" strokewidth="inherit" w="97">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="97" w="97" x="0" y="0"/>
    </background>
    <foreground>
        <strokewidth width="3"/>
        <fillstroke/>
        <path>
            <move x="23.5" y="23.5"/>
            <line x="73.5" y="73.5"/>
            <move x="73.5" y="23.5"/>
            <line x="23.5" y="73.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Cancel Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <fillstroke/>
        <strokewidth width="3"/>
        <path>
            <move x="24.5" y="24.5"/>
            <line x="74.5" y="74.5"/>
            <move x="74.5" y="24.5"/>
            <line x="24.5" y="74.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape h="10" name="Compensation" strokewidth="inherit" w="15">
    <connections/>
    <background>
        <path>
            <move x="0" y="5"/>
            <line x="7.5" y="0"/>
            <line x="7.5" y="10"/>
            <close/>
            <move x="7.5" y="5"/>
            <line x="15" y="0"/>
            <line x="15" y="10"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="97" name="Compensation End" strokewidth="inherit" w="97">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <save/>
        <ellipse h="97" w="97" x="0" y="0"/>
    </background>
    <foreground>
        <strokewidth width="3"/>
        <fillstroke/>
        <restore/>
        <rect/>
        <stroke/>
        <path>
            <move x="26.5" y="48.5"/>
            <line x="48.5" y="33.5"/>
            <line x="48.5" y="63.5"/>
            <close/>
            <move x="48.5" y="48.5"/>
            <line x="70.5" y="33.5"/>
            <line x="70.5" y="63.5"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Compensation Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <fillstroke/>
        <path>
            <move x="27.5" y="49.5"/>
            <line x="49.5" y="34.5"/>
            <line x="49.5" y="64.5"/>
            <close/>
            <move x="49.5" y="49.5"/>
            <line x="71.5" y="34.5"/>
            <line x="71.5" y="64.5"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="97" name="Error End" strokewidth="inherit" w="97">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <save/>
        <ellipse h="97" w="97" x="0" y="0"/>
    </background>
    <foreground>
        <strokewidth width="3"/>
        <fillstroke/>
        <restore/>
        <rect/>
        <stroke/>
        <path>
            <move x="26.5" y="79.5"/>
            <line x="39.5" y="24.5"/>
            <line x="58.5" y="61.5"/>
            <line x="69.5" y="18.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Error Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <stroke/>
        <path>
            <move x="27.5" y="80.5"/>
            <line x="40.5" y="25.5"/>
            <line x="59.5" y="62.5"/>
            <line x="70.5" y="19.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Gateway" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
    </connections>
    <background>
        <path>
            <move x="49.5" y="0"/>
            <line x="99" y="49.5"/>
            <line x="49.5" y="99"/>
            <line x="0" y="49.5"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Gateway AND" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
    </connections>
    <background>
        <path>
            <move x="49.5" y="0"/>
            <line x="99" y="49.5"/>
            <line x="49.5" y="99"/>
            <line x="0" y="49.5"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
        <path>
            <move x="49.5" y="19.5"/>
            <line x="49.5" y="79.5"/>
            <move x="79.5" y="49.5"/>
            <line x="19.5" y="49.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Gateway COMPLEX" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
    </connections>
    <background>
        <path>
            <move x="49.5" y="0"/>
            <line x="99" y="49.5"/>
            <line x="49.5" y="99"/>
            <line x="0" y="49.5"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
        <strokewidth width="3"/>
        <path>
            <move x="79.5" y="49.5"/>
            <line x="19.5" y="49.5"/>
            <move x="49.5" y="19.5"/>
            <line x="49.5" y="79.5"/>
            <move x="28.5" y="28.5"/>
            <line x="70.5" y="70.5"/>
            <move x="70.5" y="28.5"/>
            <line x="28.5" y="70.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Gateway OR" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
    </connections>
    <background>
        <path>
            <move x="49.5" y="0"/>
            <line x="99" y="49.5"/>
            <line x="49.5" y="99"/>
            <line x="0" y="49.5"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
        <strokewidth width="3"/>
        <ellipse h="50" w="50" x="24.5" y="24.5"/>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Gateway XOR (data)" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
    </connections>
    <background>
        <path>
            <move x="49.5" y="0"/>
            <line x="99" y="49.5"/>
            <line x="49.5" y="99"/>
            <line x="0" y="49.5"/>
            <close/>
            <move x="37.5" y="23.5"/>
            <line x="61.5" y="75.5"/>
            <move x="61.5" y="23.5"/>
            <line x="37.5" y="75.5"/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Gateway XOR (event)" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
    </connections>
    <background>
        <path>
            <move x="49.5" y="0"/>
            <line x="99" y="49.5"/>
            <line x="49.5" y="99"/>
            <line x="0" y="49.5"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="49.8" w="49.8" x="24.6" y="24.6"/>
        <stroke/>
        <ellipse h="46.2" w="46.2" x="26.4" y="26.4"/>
        <stroke/>
        <path>
            <move x="49.5" y="37.1"/>
            <line x="60.2" y="55.7"/>
            <line x="38.8" y="55.7"/>
            <close/>
            <move x="49.5" y="61.9"/>
            <line x="59.5" y="43.3"/>
            <line x="38.5" y="43.3"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="97" name="General End" strokewidth="inherit" w="97">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="97" w="97" x="0" y="0"/>
    </background>
    <foreground>
        <strokewidth width="3"/>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="General Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="General Start" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="97" name="Link End" strokewidth="inherit" w="97">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <save/>
        <ellipse h="97" w="97" x="0" y="0"/>
    </background>
    <foreground>
        <strokewidth width="3"/>
        <fillstroke/>
        <restore/>
        <rect/>
        <stroke/>
        <path>
            <move x="25.5" y="57.5"/>
            <line x="25.5" y="39.5"/>
            <line x="54.5" y="39.5"/>
            <line x="54.5" y="31.5"/>
            <line x="71.5" y="48.5"/>
            <line x="54.5" y="65.5"/>
            <line x="54.5" y="57.5"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Link Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <stroke/>
        <path>
            <move x="26.5" y="58.5"/>
            <line x="26.5" y="40.5"/>
            <line x="55.5" y="40.5"/>
            <line x="55.5" y="32.5"/>
            <line x="72.5" y="49.5"/>
            <line x="55.5" y="66.5"/>
            <line x="55.5" y="58.5"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Link Start" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <path>
            <move x="26.5" y="58.5"/>
            <line x="26.5" y="40.5"/>
            <line x="55.5" y="40.5"/>
            <line x="55.5" y="32.5"/>
            <line x="72.5" y="49.5"/>
            <line x="55.5" y="66.5"/>
            <line x="55.5" y="58.5"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape h="21.62" name="Loop" strokewidth="inherit" w="22.49">
    <connections/>
    <background>
        <path>
            <move x="5.5" y="19.08"/>
            <arc large-arc-flag="1" rx="10" ry="10" sweep-flag="1" x="10.5" x-axis-rotation="0" y="21.08"/>
            <move x="5.5" y="14.08"/>
            <line x="5.5" y="19.08"/>
            <line x="0" y="17.58"/>
        </path>
    </background>
    <foreground>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="10.39" name="Loop Marker" strokewidth="inherit" w="15">
    <connections/>
    <background>
        <path>
            <move x="0" y="1.69"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="1" x="7.5" x-axis-rotation="0" y="1.69"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="0" x="15" x-axis-rotation="0" y="1.69"/>
            <line x="15" y="8.69"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="1" x="7.5" x-axis-rotation="0" y="8.69"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="0" x="0" x-axis-rotation="0" y="8.69"/>
            <close/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
    </foreground>
</shape>
<shape h="59.28" name="Manual Task" strokewidth="inherit" w="91.4">
    <connections/>
    <background>
        <path>
            <move x="0" y="14"/>
            <arc large-arc-flag="0" rx="20" ry="20" sweep-flag="1" x="14" x-axis-rotation="0" y="0"/>
            <line x="50" y="0"/>
            <arc large-arc-flag="0" rx="6" ry="6" sweep-flag="1" x="50" x-axis-rotation="0" y="11"/>
            <line x="26" y="11"/>
            <line x="87" y="11"/>
            <arc large-arc-flag="0" rx="7" ry="7" sweep-flag="1" x="87" x-axis-rotation="0" y="24"/>
            <line x="45" y="24"/>
            <line x="87" y="24"/>
            <arc large-arc-flag="0" rx="7" ry="7" sweep-flag="1" x="87" x-axis-rotation="0" y="37"/>
            <line x="49" y="37"/>
            <line x="82" y="37"/>
            <arc large-arc-flag="0" rx="6" ry="6" sweep-flag="1" x="82" x-axis-rotation="0" y="49"/>
            <line x="48" y="49"/>
            <line x="75" y="49"/>
            <arc large-arc-flag="0" rx="5" ry="5" sweep-flag="1" x="75" x-axis-rotation="0" y="59"/>
            <line x="9" y="59"/>
            <arc large-arc-flag="0" rx="8" ry="8" sweep-flag="1" x="0" x-axis-rotation="0" y="52"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="97" name="Message End" strokewidth="inherit" w="97">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <save/>
        <ellipse h="97" w="97" x="0" y="0"/>
    </background>
    <foreground>
        <strokewidth width="3"/>
        <fillstroke/>
        <restore/>
        <rect/>
        <stroke/>
        <rect h="40" w="70" x="13.5" y="28.5"/>
        <stroke/>
        <path>
            <move x="13.5" y="28.5"/>
            <line x="48.5" y="48.5"/>
            <line x="83.5" y="28.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Message Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <stroke/>
        <rect h="40" w="70" x="14.5" y="29.5"/>
        <stroke/>
        <path>
            <move x="14.5" y="29.5"/>
            <line x="49.5" y="49.5"/>
            <line x="84.5" y="29.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Message Start" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <rect h="40" w="70" x="14.5" y="29.5"/>
        <stroke/>
        <path>
            <move x="14.5" y="29.5"/>
            <line x="49.5" y="49.5"/>
            <line x="84.5" y="29.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="97" name="Multiple End" strokewidth="inherit" w="97">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <save/>
        <ellipse h="97" w="97" x="0" y="0"/>
    </background>
    <foreground>
        <strokewidth width="3"/>
        <fillstroke/>
        <restore/>
        <rect/>
        <stroke/>
        <path>
            <move x="48.5" y="23.5"/>
            <line x="70.5" y="60.5"/>
            <line x="26.5" y="60.5"/>
            <close/>
            <move x="48.5" y="73.5"/>
            <line x="70.5" y="36.5"/>
            <line x="26.5" y="36.5"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="14" name="Multiple Instances" strokewidth="inherit" w="9">
    <connections/>
    <background>
        <path>
            <move x="0" y="0"/>
            <line x="3" y="0"/>
            <line x="3" y="14"/>
            <line x="0" y="14"/>
            <close/>
            <move x="6" y="0"/>
            <line x="9" y="0"/>
            <line x="9" y="14"/>
            <line x="6" y="14"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Multiple Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <stroke/>
        <path>
            <move x="49.5" y="24.5"/>
            <line x="71.5" y="61.5"/>
            <line x="27.5" y="61.5"/>
            <close/>
            <move x="49.5" y="74.5"/>
            <line x="71.5" y="37.5"/>
            <line x="27.5" y="37.5"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Multiple Start" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <path>
            <move x="49.5" y="24.5"/>
            <line x="71.5" y="61.5"/>
            <line x="27.5" y="61.5"/>
            <close/>
            <move x="49.5" y="74.5"/>
            <line x="71.5" y="37.5"/>
            <line x="27.5" y="37.5"/>
            <close/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Rule Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <stroke/>
        <rect h="68" w="40" x="29.5" y="15.5"/>
        <stroke/>
        <path>
            <move x="29.5" y="22.5"/>
            <line x="61.5" y="22.5"/>
            <move x="29.5" y="40.5"/>
            <line x="61.5" y="40.5"/>
            <move x="29.5" y="58.5"/>
            <line x="61.5" y="58.5"/>
            <move x="29.5" y="76.5"/>
            <line x="61.5" y="76.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Rule Start" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <rect h="68" w="40" x="29.5" y="15.5"/>
        <stroke/>
        <path>
            <move x="29.5" y="22.5"/>
            <line x="61.5" y="22.5"/>
            <move x="29.5" y="40.5"/>
            <line x="61.5" y="40.5"/>
            <move x="29.5" y="58.5"/>
            <line x="61.5" y="58.5"/>
            <move x="29.5" y="76.5"/>
            <line x="61.5" y="76.5"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape h="100" name="Script Task" strokewidth="inherit" w="73.4">
    <connections/>
    <background>
        <path>
            <move x="61.7" y="0"/>
            <arc large-arc-flag="0" rx="40" ry="40" sweep-flag="0" x="61.7" x-axis-rotation="0" y="50"/>
            <arc large-arc-flag="0" rx="40" ry="40" sweep-flag="1" x="61.7" x-axis-rotation="0" y="100"/>
            <line x="11.7" y="100"/>
            <arc large-arc-flag="0" rx="40" ry="40" sweep-flag="0" x="11.7" x-axis-rotation="0" y="50"/>
            <arc large-arc-flag="0" rx="40" ry="40" sweep-flag="1" x="11.7" x-axis-rotation="0" y="0"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
        <path>
            <move x="21.7" y="50"/>
            <line x="51.7" y="50"/>
            <move x="13.7" y="30"/>
            <line x="43.7" y="30"/>
            <move x="15.7" y="10"/>
            <line x="45.7" y="10"/>
            <move x="29.7" y="70"/>
            <line x="59.7" y="70"/>
            <move x="27.7" y="90"/>
            <line x="57.7" y="90"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape h="93.3" name="Service Task" strokewidth="inherit" w="90.9">
    <connections/>
    <background>
        <path>
            <move x="2.06" y="24.62"/>
            <line x="10.17" y="30.95"/>
            <line x="9.29" y="37.73"/>
            <line x="0" y="41.42"/>
            <line x="2.95" y="54.24"/>
            <line x="13.41" y="52.92"/>
            <line x="17.39" y="58.52"/>
            <line x="13.56" y="67.66"/>
            <line x="24.47" y="74.44"/>
            <line x="30.81" y="66.33"/>
            <line x="37.88" y="67.21"/>
            <line x="41.57" y="76.5"/>
            <line x="54.24" y="73.55"/>
            <line x="53.06" y="62.94"/>
            <line x="58.52" y="58.52"/>
            <line x="67.21" y="63.09"/>
            <line x="74.58" y="51.88"/>
            <line x="66.03" y="45.25"/>
            <line x="66.92" y="38.62"/>
            <line x="76.5" y="34.93"/>
            <line x="73.7" y="22.26"/>
            <line x="62.64" y="23.44"/>
            <line x="58.81" y="18.42"/>
            <line x="62.79" y="8.7"/>
            <line x="51.74" y="2.21"/>
            <line x="44.81" y="10.47"/>
            <line x="38.03" y="9.43"/>
            <line x="33.75" y="0"/>
            <line x="21.52" y="3.24"/>
            <line x="22.7" y="13.56"/>
            <line x="18.13" y="17.54"/>
            <line x="8.7" y="13.56"/>
            <close/>
            <move x="24.8" y="39"/>
            <arc large-arc-flag="1" rx="12" ry="12" sweep-flag="1" x="51.8" x-axis-rotation="0" y="39"/>
            <arc large-arc-flag="0" rx="12" ry="12" sweep-flag="1" x="24.8" x-axis-rotation="0" y="39"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
        <path>
            <move x="16.46" y="41.42"/>
            <line x="24.57" y="47.75"/>
            <line x="23.69" y="54.53"/>
            <line x="14.4" y="58.22"/>
            <line x="17.35" y="71.04"/>
            <line x="27.81" y="69.72"/>
            <line x="31.79" y="75.32"/>
            <line x="27.96" y="84.46"/>
            <line x="38.87" y="91.24"/>
            <line x="45.21" y="83.13"/>
            <line x="52.28" y="84.01"/>
            <line x="55.97" y="93.3"/>
            <line x="68.64" y="90.35"/>
            <line x="67.46" y="79.74"/>
            <line x="72.92" y="75.32"/>
            <line x="81.61" y="79.89"/>
            <line x="88.98" y="68.68"/>
            <line x="80.43" y="62.05"/>
            <line x="81.32" y="55.42"/>
            <line x="90.9" y="51.73"/>
            <line x="88.1" y="39.06"/>
            <line x="77.04" y="40.24"/>
            <line x="73.21" y="35.22"/>
            <line x="77.19" y="25.5"/>
            <line x="66.14" y="19.01"/>
            <line x="59.21" y="27.27"/>
            <line x="52.43" y="26.23"/>
            <line x="48.15" y="16.8"/>
            <line x="35.92" y="20.04"/>
            <line x="37.1" y="30.36"/>
            <line x="32.53" y="34.34"/>
            <line x="23.1" y="30.36"/>
            <close/>
            <move x="39.2" y="55.8"/>
            <arc large-arc-flag="1" rx="12" ry="12" sweep-flag="1" x="66.2" x-axis-rotation="0" y="55.8"/>
            <arc large-arc-flag="0" rx="12" ry="12" sweep-flag="1" x="39.2" x-axis-rotation="0" y="55.8"/>
            <close/>
        </path>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="97" name="Terminate" strokewidth="inherit" w="97">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="97" w="97" x="0" y="0"/>
    </background>
    <foreground>
        <strokewidth width="3"/>
        <fillstroke/>
        <strokewidth width="42"/>
        <ellipse h="42" w="42" x="27.5" y="27.5"/>
        <fillstroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Timer Intermediate" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="92" w="92" x="3.5" y="3.5"/>
        <stroke/>
        <ellipse h="78" w="78" x="10.5" y="10.5"/>
        <stroke/>
        <path>
            <move x="49.5" y="49.5"/>
            <line x="51.5" y="16"/>
            <move x="49.5" y="49.5"/>
            <line x="71.5" y="49.5"/>
            <move x="49.5" y="10.5"/>
            <line x="49.5" y="15.5"/>
            <move x="69" y="15.6"/>
            <line x="66.2" y="20.5"/>
            <move x="83.2" y="29.8"/>
            <line x="78.3" y="32.8"/>
            <move x="88.5" y="49.5"/>
            <line x="83.5" y="49.5"/>
            <move x="83.2" y="69.2"/>
            <line x="78.3" y="66.2"/>
            <move x="30" y="15.6"/>
            <line x="32.8" y="20.5"/>
            <move x="69" y="83.4"/>
            <line x="66.2" y="78.5"/>
            <move x="49.5" y="83.5"/>
            <line x="49.5" y="88.5"/>
            <move x="30" y="83.4"/>
            <line x="32.8" y="78.5"/>
            <move x="15.8" y="69.2"/>
            <line x="20.7" y="66.2"/>
            <move x="10.5" y="49.5"/>
            <line x="15.5" y="49.5"/>
            <move x="15.8" y="29.8"/>
            <line x="20.7" y="32.8"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape aspect="fixed" h="99" name="Timer Start" strokewidth="inherit" w="99">
    <connections>
        <constraint name="N" perimeter="0" x="0.5" y="0"/>
        <constraint name="S" perimeter="0" x="0.5" y="1"/>
        <constraint name="W" perimeter="0" x="0" y="0.5"/>
        <constraint name="E" perimeter="0" x="1" y="0.5"/>
        <constraint name="NW" perimeter="0" x="0.145" y="0.145"/>
        <constraint name="SW" perimeter="0" x="0.145" y="0.855"/>
        <constraint name="NE" perimeter="0" x="0.855" y="0.145"/>
        <constraint name="SE" perimeter="0" x="0.855" y="0.855"/>
    </connections>
    <background>
        <ellipse h="99" w="99" x="0" y="0"/>
    </background>
    <foreground>
        <fillstroke/>
        <ellipse h="78" w="78" x="10.5" y="10.5"/>
        <stroke/>
        <path>
            <move x="49.5" y="49.5"/>
            <line x="51.5" y="16"/>
            <move x="49.5" y="49.5"/>
            <line x="71.5" y="49.5"/>
            <move x="49.5" y="10.5"/>
            <line x="49.5" y="15.5"/>
            <move x="69" y="15.6"/>
            <line x="66.2" y="20.5"/>
            <move x="83.2" y="29.8"/>
            <line x="78.3" y="32.8"/>
            <move x="88.5" y="49.5"/>
            <line x="83.5" y="49.5"/>
            <move x="83.2" y="69.2"/>
            <line x="78.3" y="66.2"/>
            <move x="30" y="15.6"/>
            <line x="32.8" y="20.5"/>
            <move x="69" y="83.4"/>
            <line x="66.2" y="78.5"/>
            <move x="49.5" y="83.5"/>
            <line x="49.5" y="88.5"/>
            <move x="30" y="83.4"/>
            <line x="32.8" y="78.5"/>
            <move x="15.8" y="69.2"/>
            <line x="20.7" y="66.2"/>
            <move x="10.5" y="49.5"/>
            <line x="15.5" y="49.5"/>
            <move x="15.8" y="29.8"/>
            <line x="20.7" y="32.8"/>
        </path>
        <stroke/>
    </foreground>
</shape>
<shape h="91.81" name="User Task" strokewidth="inherit" w="94">
    <connections/>
    <background>
        <path>
            <move x="0" y="91.81"/>
            <line x="0" y="63.81"/>
            <arc large-arc-flag="0" rx="50" ry="50" sweep-flag="1" x="24" x-axis-rotation="0" y="42.81"/>
            <arc large-arc-flag="0" rx="25" ry="25" sweep-flag="1" x="33" x-axis-rotation="0" y="41.81"/>
            <arc large-arc-flag="0" rx="17" ry="17" sweep-flag="0" x="48" x-axis-rotation="0" y="58.81"/>
            <arc large-arc-flag="0" rx="17" ry="17" sweep-flag="0" x="66" x-axis-rotation="0" y="41.81"/>
            <arc large-arc-flag="0" rx="25" ry="25" sweep-flag="1" x="76.8" x-axis-rotation="0" y="42.81"/>
            <arc large-arc-flag="0" rx="35" ry="35" sweep-flag="1" x="94" x-axis-rotation="0" y="63.81"/>
            <line x="94" y="91.81"/>
            <close/>
            <move x="66" y="41.81"/>
            <arc large-arc-flag="0" rx="17" ry="17" sweep-flag="1" x="48" x-axis-rotation="0" y="58.81"/>
            <arc large-arc-flag="0" rx="17" ry="17" sweep-flag="1" x="33" x-axis-rotation="0" y="41.81"/>
            <arc large-arc-flag="0" rx="25" ry="25" sweep-flag="0" x="38" x-axis-rotation="0" y="40.81"/>
            <line x="39" y="36.81"/>
            <arc large-arc-flag="0" rx="10" ry="10" sweep-flag="1" x="32" x-axis-rotation="0" y="30.81"/>
            <arc large-arc-flag="1" rx="18" ry="12" sweep-flag="1" x="66" x-axis-rotation="0" y="30.81"/>
            <arc large-arc-flag="0" rx="12" ry="12" sweep-flag="1" x="58" x-axis-rotation="0" y="36.81"/>
            <line x="59" y="40.81"/>
            <close/>
        </path>
    </background>
    <foreground>
        <fillstroke/>
        <path>
            <move x="16" y="75.81"/>
            <line x="16" y="90.81"/>
            <move x="75" y="75.81"/>
            <line x="75" y="90.81"/>
        </path>
        <stroke/>
        <fillcolor color="#000000"/>
        <path>
            <move x="32" y="30.81"/>
            <arc large-arc-flag="0" rx="15" ry="15" sweep-flag="1" x="29" x-axis-rotation="0" y="13.81"/>
            <arc large-arc-flag="0" rx="22" ry="22" sweep-flag="1" x="48" x-axis-rotation="0" y="0.81"/>
            <arc large-arc-flag="0" rx="22" ry="22" sweep-flag="1" x="70" x-axis-rotation="0" y="13.81"/>
            <arc large-arc-flag="0" rx="15" ry="15" sweep-flag="1" x="66" x-axis-rotation="0" y="30.81"/>
            <arc large-arc-flag="0" rx="15" ry="15" sweep-flag="0" x="64" x-axis-rotation="0" y="21.81"/>
            <arc large-arc-flag="0" rx="15" ry="15" sweep-flag="0" x="50" x-axis-rotation="0" y="20.81"/>
            <arc large-arc-flag="0" rx="15" ry="15" sweep-flag="0" x="35" x-axis-rotation="0" y="21.81"/>
            <arc large-arc-flag="0" rx="15" ry="15" sweep-flag="0" x="32" x-axis-rotation="0" y="30.81"/>
            <close/>
        </path>
        <fillstroke/>
    </foreground>
</shape>
</shapes>`;

const FLOWCHART_XML = `<shapes name="mxGraph.flowchart">
<shape name="Annotation 1" h="98" w="50" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0" y="0" perimeter="0" name="NW"/>
<constraint x="0" y="1" perimeter="0" name="SW"/>
<constraint x="1" y="0" perimeter="0" name="NE"/>
<constraint x="1" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="50" y="0"/>
<line x="0" y="0"/>
<line x="0" y="98"/>
<line x="50" y="98"/>
</path>
</background>
<foreground>
<stroke/>
</foreground>
</shape>
<shape name="Annotation 2" h="98" w="100" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="1" y="0" perimeter="0" name="NE"/>
<constraint x="1" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="100" y="0"/>
<line x="50" y="0"/>
<line x="50" y="98"/>
<line x="100" y="98"/>
</path>
</background>
<foreground>
<stroke/>
<path>
<move x="0" y="49"/>
<line x="50" y="49"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Card" h="60" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.1" y="0.16" perimeter="0" name="NW"/>
<constraint x="0.015" y="0.98" perimeter="0" name="SW"/>
<constraint x="0.985" y="0.02" perimeter="0" name="NE"/>
<constraint x="0.985" y="0.98" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="19" y="0"/>
<line x="93" y="0"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="98" y="5"/>
<line x="98" y="55"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="93" y="60"/>
<line x="5" y="60"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="55"/>
<line x="0" y="20"/>
<line x="19" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Collate" h="98" w="96.82" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.02" perimeter="0" name="NW"/>
<constraint x="0" y="0.98" perimeter="0" name="SW"/>
<constraint x="1" y="0.02" perimeter="0" name="NE"/>
<constraint x="1" y="0.98" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="92.41" y="0"/>
<arc rx="6" ry="3.5" x-axis-rotation="-15" large-arc-flag="0" sweep-flag="1" x="95.41" y="5"/>
<line x="1.41" y="93"/>
<arc rx="6" ry="3.5" x-axis-rotation="-15" large-arc-flag="0" sweep-flag="0" x="4.41" y="98"/>
<line x="92.41" y="98"/>
<arc rx="6" ry="3.5" x-axis-rotation="15" large-arc-flag="0" sweep-flag="0" x="95.41" y="93"/>
<line x="1.41" y="5"/>
<arc rx="6" ry="3.5" x-axis-rotation="15" large-arc-flag="0" sweep-flag="1" x="4.41" y="0"/>
<line x="92.41" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Data" h="60.24" w="98.77" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0.095" y="0.5" perimeter="0" name="W"/>
<constraint x="0.905" y="0.5" perimeter="0" name="E"/>
<constraint x="0.23" y="0.02" perimeter="0" name="NW"/>
<constraint x="0.015" y="0.98" perimeter="0" name="SW"/>
<constraint x="0.985" y="0.02" perimeter="0" name="NE"/>
<constraint x="0.77" y="0.98" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="19.37" y="5.12"/>
<arc rx="6" ry="12" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="24.37" y="0.12"/>
<line x="93.37" y="0.12"/>
<arc rx="5" ry="4" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="98.37" y="5.12"/>
<line x="79.37" y="55.12"/>
<arc rx="6" ry="12" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="74.37" y="60.12"/>
<line x="4.37" y="60.12"/>
<arc rx="5" ry="4" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0.37" y="55.12"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Database" h="60" w="60" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0" y="0.15" perimeter="0" name="NW"/>
<constraint x="0" y="0.85" perimeter="0" name="SW"/>
<constraint x="1" y="0.15" perimeter="0" name="NE"/>
<constraint x="1" y="0.85" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="50"/>
<line x="0" y="10"/>
<arc rx="30" ry="10" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="60" y="10"/>
<line x="60" y="50"/>
<arc rx="30" ry="10" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="50"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<path>
<move x="0" y="10"/>
<arc rx="30" ry="10" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="60" y="10"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Decision" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="50" y="0"/>
<line x="100" y="50"/>
<line x="50" y="100"/>
<line x="0" y="50"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Delay" h="60" w="98.25" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.02" y="0.015" perimeter="0" name="NW"/>
<constraint x="0.02" y="0.985" perimeter="0" name="SW"/>
<constraint x="0.81" y="0" perimeter="0" name="NE"/>
<constraint x="0.81" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="5"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="5" y="0"/>
<line x="79" y="0"/>
<arc rx="33" ry="33" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="79" y="60"/>
<line x="5" y="60"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="55"/>
<line x="0" y="5"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Direct Data" h="60" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.08" y="0" perimeter="0" name="NW"/>
<constraint x="0.08" y="1" perimeter="0" name="SW"/>
<constraint x="0.91" y="0" perimeter="0" name="NE"/>
<constraint x="0.91" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="9" y="0"/>
<line x="89" y="0"/>
<arc rx="9" ry="30" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="89" y="60"/>
<line x="9" y="60"/>
<arc rx="9" ry="30" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="9" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<path>
<move x="89" y="0"/>
<arc rx="9" ry="30" x-axis-rotation="0" large-arc-flag="1" sweep-flag="0" x="89" y="60"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Display" h="60" w="98.25" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.2" y="0.14" perimeter="0" name="NW"/>
<constraint x="0.2" y="0.86" perimeter="0" name="SW"/>
<constraint x="0.92" y="0.14" perimeter="0" name="NE"/>
<constraint x="0.92" y="0.86" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="30"/>
<arc rx="60" ry="60" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="39" y="0"/>
<line x="79" y="0"/>
<arc rx="33" ry="33" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="79" y="60"/>
<line x="39" y="60"/>
<arc rx="60" ry="60" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="30"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Document" h="60.9" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="0.9" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.02" y="0.015" perimeter="0" name="NW"/>
<constraint x="0" y="0.9" perimeter="0" name="SW"/>
<constraint x="0.98" y="0.015" perimeter="0" name="NE"/>
<constraint x="1" y="0.9" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="5"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="5" y="0"/>
<line x="93" y="0"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="98" y="5"/>
<line x="98" y="55"/>
<arc rx="70" ry="70" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="49" y="55"/>
<arc rx="70" ry="70" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="55"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Extract or Measurement" h="61.03" w="95.34" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0.22" y="0.5" perimeter="0" name="W"/>
<constraint x="0.78" y="0.5" perimeter="0" name="E"/>
<constraint x="0.01" y="0.97" perimeter="0" name="SW"/>
<constraint x="0.99" y="0.97" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="3.64" y="61.02"/>
<line x="91.64" y="61.02"/>
<arc rx="6" ry="4" x-axis-rotation="30" large-arc-flag="0" sweep-flag="0" x="94.64" y="56.02"/>
<line x="49.64" y="1.02"/>
<arc rx="3" ry="3" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="45.64" y="1.02"/>
<line x="0.64" y="56.02"/>
<arc rx="6" ry="4" x-axis-rotation="-35" large-arc-flag="0" sweep-flag="0" x="3.64" y="61.02"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Internal Storage" h="70" w="70" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.02" y="0.015" perimeter="0" name="NW"/>
<constraint x="0.02" y="0.985" perimeter="0" name="SW"/>
<constraint x="0.98" y="0.015" perimeter="0" name="NE"/>
<constraint x="0.98" y="0.985" perimeter="0" name="SE"/>
</connections>
<background>
<roundrect x="0" y="0" w="70" h="70" arcsize="7.142857142857142"/>
</background>
<foreground>
<fillstroke/>
<path>
<move x="0" y="15"/>
<line x="70" y="15"/>
</path>
<stroke/>
<path>
<move x="15" y="0"/>
<line x="15" y="70"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Loop Limit" h="60" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.1" y="0.15" perimeter="0" name="NW"/>
<constraint x="0.02" y="0.985" perimeter="0" name="SW"/>
<constraint x="0.9" y="0.15" perimeter="0" name="NE"/>
<constraint x="0.98" y="0.985" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="19" y="0"/>
<line x="79" y="0"/>
<line x="98" y="20"/>
<line x="98" y="55"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="93" y="60"/>
<line x="5" y="60"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="55"/>
<line x="0" y="20"/>
<line x="19" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Manual Input" h="60" w="98.05" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0.195" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.02" y="0.985" perimeter="0" name="SW"/>
<constraint x="0.98" y="0.015" perimeter="0" name="NE"/>
<constraint x="0.98" y="0.985" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="25"/>
<line x="93" y="0"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="98" y="5"/>
<line x="98" y="55"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="94" y="60"/>
<line x="5" y="60"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="55"/>
<line x="0" y="25"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Manual Operation" h="60.04" w="98.79" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0.1" y="0.5" perimeter="0" name="W"/>
<constraint x="0.9" y="0.5" perimeter="0" name="E"/>
<constraint x="0.02" y="0.015" perimeter="0" name="NW"/>
<constraint x="0.22" y="0.985" perimeter="0" name="SW"/>
<constraint x="0.98" y="0.015" perimeter="0" name="NE"/>
<constraint x="0.78" y="0.985" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0.39" y="5.04"/>
<arc rx="5" ry="4" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="5.39" y="0.04"/>
<line x="93.39" y="0.04"/>
<arc rx="5" ry="4" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="98.39" y="5.04"/>
<line x="79.39" y="55.04"/>
<arc rx="6.5" ry="6.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="74.39" y="60.04"/>
<line x="24.39" y="60.04"/>
<arc rx="6.5" ry="6.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="19.39" y="55.04"/>
<line x="0.39" y="5.04"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Merge or Storage" h="61.03" w="95.34" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0" y="0" perimeter="0" name="NW"/>
<constraint x="0" y="1" perimeter="0" name="SW"/>
<constraint x="1" y="0" perimeter="0" name="NE"/>
<constraint x="1" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="3.64" y="0.01"/>
<line x="91.64" y="0.01"/>
<arc rx="6" ry="4" x-axis-rotation="-30" large-arc-flag="0" sweep-flag="1" x="94.64" y="5.01"/>
<line x="49.64" y="60.01"/>
<arc rx="3" ry="3" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="45.64" y="60.01"/>
<line x="0.64" y="5.01"/>
<arc rx="6" ry="4" x-axis-rotation="35" large-arc-flag="0" sweep-flag="1" x="3.64" y="0.01"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Multi-Document" h="60.28" w="88" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="0.88" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.08" y="0.1" perimeter="0" name="NW"/>
<constraint x="0" y="0.91" perimeter="0" name="SW"/>
<constraint x="0.98" y="0.02" perimeter="0" name="NE"/>
<constraint x="0.885" y="0.91" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="10" y="5"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="15" y="0"/>
<line x="83" y="0"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="88" y="5"/>
<line x="88" y="45"/>
<arc rx="50" ry="50" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="49" y="45"/>
<arc rx="50" ry="50" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="10" y="45"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<path>
<move x="5" y="10"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="10" y="5"/>
<line x="78" y="5"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="83" y="10"/>
<line x="83" y="50"/>
<arc rx="50" ry="50" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="44" y="50"/>
<arc rx="50" ry="50" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="5" y="50"/>
<close/>
</path>
<fillstroke/>
<path>
<move x="0" y="15"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="5" y="10"/>
<line x="73" y="10"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="78" y="15"/>
<line x="78" y="55"/>
<arc rx="50" ry="50" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="39" y="55"/>
<arc rx="50" ry="50" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="55"/>
<close/>
</path>
<fillstroke/>
</foreground>
</shape>
<shape name="Off-page Reference" h="60" w="60" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0" y="0" perimeter="0" name="NW"/>
<constraint x="1" y="0" perimeter="0" name="NE"/>
</connections>
<background>
<path>
<move x="0" y="0"/>
<line x="60" y="0"/>
<line x="60" y="30"/>
<line x="30" y="60"/>
<line x="0" y="30"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="On-page Reference" h="60" w="60" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="0.855" y="0.855" perimeter="0" name="SE"/>
</connections>
<background>
<ellipse x="0" y="0" w="60" h="60"/>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Or" h="70" w="70" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="0.855" y="0.855" perimeter="0" name="SE"/>
</connections>
<background>
<ellipse x="0" y="0" w="70" h="70"/>
</background>
<foreground>
<fillstroke/>
<path>
<move x="10" y="60"/>
<line x="60" y="10"/>
</path>
<stroke/>
<path>
<move x="10" y="10"/>
<line x="60" y="60"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Paper Tape" h="61.81" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0.09" perimeter="0" name="N"/>
<constraint x="0.5" y="0.91" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0" y="0.09" perimeter="0" name="NW"/>
<constraint x="0" y="0.91" perimeter="0" name="SW"/>
<constraint x="1" y="0.09" perimeter="0" name="NE"/>
<constraint x="1" y="0.91" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="0" y="5.9"/>
<arc rx="70" ry="70" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="49" y="5.9"/>
<arc rx="70" ry="70" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="98" y="5.9"/>
<line x="98" y="55.9"/>
<arc rx="70" ry="70" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="49" y="55.9"/>
<arc rx="70" ry="70" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0" y="55.9"/>
<line x="0" y="5.9"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Parallel Mode" h="40" w="94" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0" y="0" perimeter="0" name="NW"/>
<constraint x="0" y="1" perimeter="0" name="SW"/>
<constraint x="1" y="0" perimeter="0" name="NE"/>
<constraint x="1" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<save/>
<fillcolor color="#ffff00"/>
<path>
<move x="47" y="15"/>
<line x="52" y="20"/>
<line x="47" y="25"/>
<line x="42" y="20"/>
<line x="47" y="15"/>
<close/>
<move x="27" y="15"/>
<line x="32" y="20"/>
<line x="27" y="25"/>
<line x="22" y="20"/>
<line x="27" y="15"/>
<close/>
<move x="67" y="15"/>
<line x="72" y="20"/>
<line x="67" y="25"/>
<line x="62" y="20"/>
<line x="67" y="15"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<restore/>
<path>
<move x="0" y="0"/>
<line x="94" y="0"/>
</path>
<stroke/>
<path>
<move x="0" y="40"/>
<line x="94" y="40"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Predefined Process" h="60" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.02" y="0.015" perimeter="0" name="NW"/>
<constraint x="0.02" y="0.985" perimeter="0" name="SW"/>
<constraint x="0.98" y="0.015" perimeter="0" name="NE"/>
<constraint x="0.98" y="0.985" perimeter="0" name="SE"/>
</connections>
<background>
<roundrect x="0" y="0" w="98" h="60" arcsize="6.717687074829931"/>
</background>
<foreground>
<fillstroke/>
<path>
<move x="14" y="0"/>
<line x="14" y="60"/>
</path>
<stroke/>
<path>
<move x="84" y="0"/>
<line x="84" y="60"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Preparation" h="60" w="97.11" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.26" y="0.02" perimeter="0" name="NW"/>
<constraint x="0.26" y="0.98" perimeter="0" name="SW"/>
<constraint x="0.74" y="0.02" perimeter="0" name="NE"/>
<constraint x="0.74" y="0.98" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="20.56" y="5"/>
<arc rx="15" ry="15" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="31.56" y="0"/>
<line x="65.56" y="0"/>
<arc rx="15" ry="15" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="76.56" y="5"/>
<line x="96.56" y="28"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="96.56" y="32"/>
<line x="76.56" y="55"/>
<arc rx="15" ry="15" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="65.56" y="60"/>
<line x="31.56" y="60"/>
<arc rx="15" ry="15" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="20.56" y="55"/>
<line x="0.56" y="32"/>
<arc rx="5" ry="5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="0.56" y="28"/>
<line x="20.56" y="5"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Process" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<roundrect x="0" y="0" w="100" h="100" arcsize="6"/>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Sequential Data" h="99" w="99" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="1" y="1" perimeter="0" name="SE"/>
</connections>
<background>
<ellipse x="0" y="0" w="99" h="99"/>
</background>
<foreground>
<fillstroke/>
<path>
<move x="49.5" y="99"/>
<line x="99" y="99"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Sort" h="98" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="51" y="1"/>
<line x="97" y="47"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="97" y="51"/>
<line x="51" y="97"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="47" y="97"/>
<line x="1" y="51"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="1" y="47"/>
<line x="47" y="1"/>
<arc rx="2.5" ry="2.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="51" y="1"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
<path>
<move x="0" y="49"/>
<line x="98" y="49"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Start 1" h="60" w="99" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="0.855" y="0.855" perimeter="0" name="SE"/>
</connections>
<background>
<ellipse x="0" y="0" w="99" h="60"/>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Start 2" h="99" w="99" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="0.855" y="0.855" perimeter="0" name="SE"/>
</connections>
<background>
<ellipse x="0" y="0" w="99" h="99"/>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Stored Data" h="60" w="96.51" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="0.93" y="0.5" perimeter="0" name="E"/>
<constraint x="0.1" y="0" perimeter="0" name="NW"/>
<constraint x="0.1" y="1" perimeter="0" name="SW"/>
<constraint x="0.995" y="0.01" perimeter="0" name="NE"/>
<constraint x="0.995" y="0.99" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="10" y="0"/>
<line x="96" y="0"/>
<arc rx="1.5" ry="1.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="96" y="2"/>
<arc rx="10" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="0" x="96" y="58"/>
<arc rx="1.5" ry="1.5" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="96" y="60"/>
<line x="10" y="60"/>
<arc rx="10" ry="30" x-axis-rotation="0" large-arc-flag="1" sweep-flag="1" x="10" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Summing Function" h="70" w="70" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.145" y="0.145" perimeter="0" name="NW"/>
<constraint x="0.145" y="0.855" perimeter="0" name="SW"/>
<constraint x="0.855" y="0.145" perimeter="0" name="NE"/>
<constraint x="0.855" y="0.855" perimeter="0" name="SE"/>
</connections>
<background>
<ellipse x="0" y="0" w="70" h="70"/>
</background>
<foreground>
<fillstroke/>
<path>
<move x="0" y="35"/>
<line x="70" y="35"/>
</path>
<stroke/>
<path>
<move x="35" y="0"/>
<line x="35" y="70"/>
</path>
<stroke/>
</foreground>
</shape>
<shape name="Terminator" h="60" w="98" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0.5" y="0" perimeter="0" name="N"/>
<constraint x="0.5" y="1" perimeter="0" name="S"/>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
<constraint x="0.11" y="0.11" perimeter="0" name="NW"/>
<constraint x="0.11" y="0.89" perimeter="0" name="SW"/>
<constraint x="0.89" y="0.11" perimeter="0" name="NE"/>
<constraint x="0.89" y="0.89" perimeter="0" name="SE"/>
</connections>
<background>
<path>
<move x="30" y="0"/>
<line x="68" y="0"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="68" y="60"/>
<line x="30" y="60"/>
<arc rx="30" ry="30" x-axis-rotation="0" large-arc-flag="0" sweep-flag="1" x="30" y="0"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
<shape name="Transfer" h="70" w="97.5" aspect="variable" strokewidth="inherit">
<connections>
<constraint x="0" y="0.5" perimeter="0" name="W"/>
<constraint x="1" y="0.5" perimeter="0" name="E"/>
</connections>
<background>
<path>
<move x="0" y="20"/>
<line x="59" y="20"/>
<line x="59" y="0"/>
<line x="97.5" y="35"/>
<line x="59" y="70"/>
<line x="59" y="50"/>
<line x="0" y="50"/>
<close/>
</path>
</background>
<foreground>
<fillstroke/>
</foreground>
</shape>
</shapes>`;

export const STENCIL_GROUPS: StencilGroup[] = [
  { name: 'Arrows', prefix: 'mxGraph.arrows', xml: ARROWS_XML, shapes: [] },
  { name: 'Extended Basic', prefix: 'mxgraph.basic', xml: BASIC_XML, shapes: [] },
  { name: 'BPMN', prefix: 'mxgraph.bpmn', xml: BPMN_XML, shapes: [] },
  { name: 'Flowchart', prefix: 'mxGraph.flowchart', xml: FLOWCHART_XML, shapes: [] }
];

/** stencil XML 을 maxgraph 레지스트리에 등록하고 팔레트 목록을 채운다. */
export const registerAllStencils = () => {
  const parser = new DOMParser();
  STENCIL_GROUPS.forEach(group => {
    const xmlDoc = parser.parseFromString(group.xml, 'text/xml');
    const shapeElements = xmlDoc.querySelectorAll('shape');
    shapeElements.forEach((shapeEl) => {
      const shapeName = shapeEl.getAttribute('name') ?? '';
      const stencil = new StencilShape(shapeEl);
      const registryName = `${group.prefix}.${shapeName.toLowerCase().replace(/ /g, '_')}`;
      StencilShapeRegistry.add(registryName, stencil);
      
      group.shapes.push({
        label: shapeName,
        registryName: registryName,
        w: Number.parseFloat(shapeEl.getAttribute('w') ?? '') || 80,
        h: Number.parseFloat(shapeEl.getAttribute('h') ?? '') || 60
      });
    });
  });
};
