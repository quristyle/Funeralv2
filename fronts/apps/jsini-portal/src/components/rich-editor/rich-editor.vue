<script lang="ts" setup>
/**
 * 서식 편집기 (tiptap).
 *
 * 공지 본문처럼 HTML 을 그대로 저장하는 자리에 쓴다. 저장 값은 HTML 문자열이다.
 *
 * **이미지는 붙여넣는 즉시 실제 파일로 보관한다.**
 * 클립보드 이미지를 base64 로 본문에 박아 두면 공지 한 건이 수 MB 가 되고,
 * 목록 조회 때마다 그 덩어리를 함께 실어 나르게 된다.
 * 그래서 붙여넣기·드래그·버튼 어느 경로로 넣든 FileServer 에 먼저 올리고,
 * 돌아온 경로로 `<img src="/api/file/download/id/...">` 를 심는다.
 *
 * 헬프데스크 요청 등록 화면은 다른 방식이다 — base64 로 넣고 서버가 저장할 때
 * 파일로 바꾼다(`FileUtil.SaveImageToFile`). 그쪽은 이식해 온 구조를 그대로 살린 것이고,
 * 포털에는 파일 전용 서비스(FileServer)가 이미 있어 여기서는 곧바로 올린다.
 */
import { onBeforeUnmount, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { EditorContent, useEditor } from '@tiptap/vue-3';
// tiptap 이 기본으로 주는 떠 있는 UI. 위치 계산(@floating-ui)까지 들어 있다.
// FloatingMenu(빈 줄에서 뜨는 삽입 메뉴)는 쓰지 않는다 — 글을 쓰다 줄을 바꿀 때마다
// 끼어들어 방해가 된다. 삽입은 위쪽 도구 모음에서 한다.
import { BubbleMenu } from '@tiptap/vue-3/menus';
import { CharacterCount } from '@tiptap/extensions';
import Highlight from '@tiptap/extension-highlight';
import Image from '@tiptap/extension-image';
import { TaskItem, TaskList } from '@tiptap/extension-list';
import { TableKit } from '@tiptap/extension-table';
import TextAlign from '@tiptap/extension-text-align';
import { TextStyleKit } from '@tiptap/extension-text-style';
import Placeholder from '@tiptap/extension-placeholder';
import StarterKit from '@tiptap/starter-kit';
import { Button, message, Spin, Tooltip } from 'ant-design-vue';

import { requestClient } from '#/api/request';

const props = withDefaults(
  defineProps<{
    /** 편집 영역 최소 높이(px) */
    minHeight?: number;
    /** 비어 있을 때 표시할 안내 문구 */
    placeholder?: string;
    /** 업로드 구분. FileServer 의 bizType 으로 그대로 넘어간다. */
    bizType?: string;
    /** 이미지 최대 크기(MB) */
    maxImageMb?: number;
    readonly?: boolean;
    /**
     * 도구 모음 크기.
     *  - `full`    : 서식·문단·정렬·삽입·기록 전부 (공지 본문처럼 긴 글)
     *  - `compact` : 한 줄에 담기는 만큼만 (댓글처럼 짧은 글)
     *  - `none`    : 도구 모음 없이. 떠 있는 메뉴로만 쓴다
     *
     * 어느 쪽이든 글자를 선택하면 뜨는 서식 메뉴는 그대로 동작한다.
     */
    toolbar?: 'compact' | 'full' | 'none';
  }>(),
  {
    minHeight: 260,
    placeholder: '내용을 입력하세요. 이미지는 붙여넣기로 바로 넣을 수 있습니다.',
    bizType: 'editor',
    maxImageMb: 10,
    readonly: false,
    toolbar: 'full',
  },
);

const modelValue = defineModel<string>({ default: '' });

/** 지금 올리고 있는 이미지 수. 0 보다 크면 편집 영역에 진행 표시를 덮는다. */
const uploading = ref(0);
const fileInputRef = ref<HTMLInputElement | null>(null);

const editor = useEditor({
  content: modelValue.value || '',
  editable: !props.readonly,
  extensions: [
    StarterKit.configure({
      link: { openOnClick: false },
    }),
    Image.configure({
      // base64 를 막는다. 이미지는 반드시 업로드를 거쳐 경로로만 들어온다.
      allowBase64: false,
      HTMLAttributes: { style: 'max-width:100%;height:auto' },
    }),
    Placeholder.configure({ placeholder: () => props.placeholder }),
    // 문단 정렬 · 형광펜 · 글자색(TextStyleKit 이 색·크기·글꼴을 함께 담고 있다)
    TextAlign.configure({ types: ['heading', 'paragraph'] }),
    Highlight.configure({ multicolor: true }),
    TextStyleKit,
    // 체크 목록과 표. 공지에서 실제로 자주 쓰는 두 가지다.
    TaskList,
    TaskItem.configure({ nested: true }),
    TableKit.configure({ table: { resizable: true } }),
    CharacterCount,
  ],
  editorProps: {
    attributes: {
      class: 'jsini-rich-editor-content',
    },
    handleDrop: (_view, event) => handleImageEvent(event, event.dataTransfer),
    handlePaste: (_view, event) => handleImageEvent(event, event.clipboardData),
  },
  onUpdate: ({ editor: instance }) => {
    // 내용이 없으면 tiptap 은 '<p></p>' 를 준다. 빈 값으로 다뤄야
    // 부모의 "비어 있는가" 판단이 어긋나지 않는다.
    modelValue.value = instance.isEmpty ? '' : instance.getHTML();
  },
});

/**
 * 붙여넣기·드롭 이벤트에서 이미지를 꺼내 업로드한다.
 * 이미지가 하나라도 있으면 true 를 돌려줘 tiptap 의 기본 처리를 막는다.
 */
function handleImageEvent(event: Event, data: DataTransfer | null) {
  const files = [...(data?.files ?? [])].filter((f) =>
    f.type.startsWith('image/'),
  );
  if (files.length === 0) return false;

  event.preventDefault();
  files.forEach((file) => uploadAndInsert(file));
  return true;
}

/** 이미지 한 장을 올리고 편집기에 끼워 넣는다. */
async function uploadAndInsert(file: File) {
  const limit = props.maxImageMb * 1024 * 1024;
  if (file.size > limit) {
    message.warning(`이미지는 ${props.maxImageMb}MB 이하만 넣을 수 있습니다.`);
    return;
  }

  uploading.value += 1;
  try {
    const res: any = await requestClient.upload(
      `/file/upload?bizType=${encodeURIComponent(props.bizType)}`,
      { file },
    );

    // 파일 서비스는 응답을 result 로 감싸 보내기도 하고 그대로 주기도 한다.
    const data = res?.result?.[0] ?? res?.result ?? res;
    const id = data?.id;
    if (!id) throw new Error('업로드 응답에 파일 아이디가 없습니다.');

    const src = data.downloadUrl || `/api/file/download/id/${id}`;
    editor.value
      ?.chain()
      .focus()
      .setImage({ alt: data.originalName ?? file.name, src })
      .run();
  } catch (error) {
    console.error(error);
    message.error(`이미지를 올리지 못했습니다: ${file.name}`);
  } finally {
    uploading.value -= 1;
  }
}

/** 도구 모음의 이미지 버튼 */
function pickImage() {
  fileInputRef.value?.click();
}

function onPickedImage(event: Event) {
  const input = event.target as HTMLInputElement;
  [...(input.files ?? [])]
    .filter((f) => f.type.startsWith('image/'))
    .forEach((file) => uploadAndInsert(file));
  // 같은 파일을 다시 골라도 change 가 나도록 비운다.
  input.value = '';
}

/** 링크를 걸거나 해제한다. */
function toggleLink() {
  const instance = editor.value;
  if (!instance) return;

  if (instance.isActive('link')) {
    instance.chain().focus().unsetLink().run();
    return;
  }

  // eslint-disable-next-line no-alert
  const url = window.prompt('연결할 주소를 입력하세요', 'https://');
  if (!url) return;
  instance.chain().focus().setLink({ href: url, target: '_blank' }).run();
}

// 바깥에서 값을 바꾼 경우(등록 폼 초기화, 수정 열기)에만 편집기에 밀어 넣는다.
watch(modelValue, (value) => {
  const instance = editor.value;
  if (!instance) return;
  const current = instance.isEmpty ? '' : instance.getHTML();
  if (current === (value || '')) return;
  instance.commands.setContent(value || '', { emitUpdate: false });
});

watch(
  () => props.readonly,
  (value) => editor.value?.setEditable(!value),
);

onBeforeUnmount(() => editor.value?.destroy());

/** 도구 모음 버튼 하나 */
interface ToolButton {
  icon: string;
  title: string;
  isActive?: () => boolean;
  run: () => void;
}

/** editor 가 준비된 뒤에만 눌린다(버튼이 :disabled 로 막혀 있다). */
const chain = () => editor.value?.chain().focus();
const active = (name: string, attrs?: Record<string, any>) =>
  Boolean(editor.value?.isActive(name, attrs));

/** 글자 · 문단 서식 */
const formatTools = (): ToolButton[] => [
  {
    icon: 'lucide:bold',
    title: '굵게',
    isActive: () => active('bold'),
    run: () => chain()?.toggleBold().run(),
  },
  {
    icon: 'lucide:italic',
    title: '기울임',
    isActive: () => active('italic'),
    run: () => chain()?.toggleItalic().run(),
  },
  {
    icon: 'lucide:underline',
    title: '밑줄',
    isActive: () => active('underline'),
    run: () => chain()?.toggleUnderline().run(),
  },
  {
    icon: 'lucide:strikethrough',
    title: '취소선',
    isActive: () => active('strike'),
    run: () => chain()?.toggleStrike().run(),
  },
  {
    icon: 'lucide:highlighter',
    title: '형광펜',
    isActive: () => active('highlight'),
    run: () => chain()?.toggleHighlight({ color: '#fff3a3' }).run(),
  },
];

/** 문단 구조 */
const blockTools = (): ToolButton[] => [
  {
    icon: 'lucide:heading-2',
    title: '제목 (큰 제목)',
    isActive: () => active('heading', { level: 2 }),
    run: () => chain()?.toggleHeading({ level: 2 }).run(),
  },
  {
    icon: 'lucide:heading-3',
    title: '제목 (작은 제목)',
    isActive: () => active('heading', { level: 3 }),
    run: () => chain()?.toggleHeading({ level: 3 }).run(),
  },
  {
    icon: 'lucide:list',
    title: '글머리 목록',
    isActive: () => active('bulletList'),
    run: () => chain()?.toggleBulletList().run(),
  },
  {
    icon: 'lucide:list-ordered',
    title: '번호 목록',
    isActive: () => active('orderedList'),
    run: () => chain()?.toggleOrderedList().run(),
  },
  {
    icon: 'lucide:list-checks',
    title: '체크 목록',
    isActive: () => active('taskList'),
    run: () => chain()?.toggleTaskList().run(),
  },
  {
    icon: 'lucide:quote',
    title: '인용',
    isActive: () => active('blockquote'),
    run: () => chain()?.toggleBlockquote().run(),
  },
  {
    icon: 'lucide:code',
    title: '코드 블록',
    isActive: () => active('codeBlock'),
    run: () => chain()?.toggleCodeBlock().run(),
  },
];

/** 정렬 */
const alignTools = (): ToolButton[] => [
  {
    icon: 'lucide:align-left',
    title: '왼쪽 정렬',
    isActive: () => Boolean(editor.value?.isActive({ textAlign: 'left' })),
    run: () => chain()?.setTextAlign('left').run(),
  },
  {
    icon: 'lucide:align-center',
    title: '가운데 정렬',
    isActive: () => Boolean(editor.value?.isActive({ textAlign: 'center' })),
    run: () => chain()?.setTextAlign('center').run(),
  },
  {
    icon: 'lucide:align-right',
    title: '오른쪽 정렬',
    isActive: () => Boolean(editor.value?.isActive({ textAlign: 'right' })),
    run: () => chain()?.setTextAlign('right').run(),
  },
];

/** 삽입 */
const insertTools = (): ToolButton[] => [
  {
    icon: 'lucide:link',
    title: '링크',
    isActive: () => active('link'),
    run: toggleLink,
  },
  {
    icon: 'lucide:image',
    title: '이미지 넣기 (붙여넣기·드래그도 됩니다)',
    run: pickImage,
  },
  {
    icon: 'lucide:table',
    title: '표 넣기 (3×3)',
    run: () =>
      chain()
        ?.insertTable({ cols: 3, rows: 3, withHeaderRow: true })
        .run(),
  },
  {
    icon: 'lucide:minus',
    title: '구분선',
    run: () => chain()?.setHorizontalRule().run(),
  },
];

/** 되돌리기 · 서식 지우기 */
const historyTools = (): ToolButton[] => [
  {
    icon: 'lucide:remove-formatting',
    title: '서식 지우기',
    run: () => chain()?.unsetAllMarks().clearNodes().run(),
  },
  {
    icon: 'lucide:undo-2',
    title: '되돌리기',
    run: () => chain()?.undo().run(),
  },
  {
    icon: 'lucide:redo-2',
    title: '다시 실행',
    run: () => chain()?.redo().run(),
  },
];

/** 표 안에 있을 때만 뜨는 조작 */
const tableTools = (): ToolButton[] => [
  {
    icon: 'lucide:between-vertical-start',
    title: '왼쪽에 열 추가',
    run: () => chain()?.addColumnBefore().run(),
  },
  {
    icon: 'lucide:between-vertical-end',
    title: '오른쪽에 열 추가',
    run: () => chain()?.addColumnAfter().run(),
  },
  {
    icon: 'lucide:between-horizontal-start',
    title: '위에 행 추가',
    run: () => chain()?.addRowBefore().run(),
  },
  {
    icon: 'lucide:between-horizontal-end',
    title: '아래에 행 추가',
    run: () => chain()?.addRowAfter().run(),
  },
  {
    icon: 'lucide:merge',
    title: '셀 병합 · 분리',
    run: () => chain()?.mergeOrSplit().run(),
  },
  {
    icon: 'lucide:trash-2',
    title: '표 삭제',
    run: () => chain()?.deleteTable().run(),
  },
];

/** 글자색. 네이티브 색상 선택기를 그대로 쓴다 — 별도 팔레트 부품이 필요 없다. */
function onPickColor(event: Event) {
  const value = (event.target as HTMLInputElement).value;
  chain()?.setColor(value).run();
}

/** 지금 글자수 (CharacterCount 확장이 준다) */
const charCount = () => editor.value?.storage.characterCount?.characters?.() ?? 0;

/** 표 안에 커서가 있는가 — 표 조작 메뉴를 띄울지 판단한다. */
const inTable = () => Boolean(editor.value?.isActive('table'));

/**
 * 화면에 그릴 도구 묶음.
 * compact 는 한 줄을 넘기지 않도록 자주 쓰는 것만 남긴다 —
 * 굵게·기울임·밑줄·취소선·형광펜 / 목록·번호목록 / 링크·이미지 / 되돌리기.
 */
function toolGroups(): ToolButton[][] {
  if (props.toolbar === 'none') return [];

  if (props.toolbar === 'compact') {
    const block = blockTools();
    const insert = insertTools();
    const history = historyTools();
    return [
      formatTools(),
      // 글머리 목록 · 번호 목록
      block.slice(2, 4),
      // 링크 · 이미지
      insert.slice(0, 2),
      // 되돌리기
      history.slice(1, 2),
    ];
  }

  return [
    formatTools(),
    blockTools(),
    alignTools(),
    insertTools(),
    historyTools(),
  ];
}

/** 입력 내용을 비운다. 댓글 폼이 등록 후 호출한다. */
function clear() {
  editor.value?.commands.clearContent(true);
  modelValue.value = '';
}

defineExpose({ clear, editor });

</script>

<template>
  <div
    class="border-border overflow-hidden rounded-md border"
    :class="readonly ? 'opacity-70' : ''"
  >
    <!-- 위쪽 도구 모음. 묶음 사이에 선을 둬서 눈이 덜 피로하게 한다. -->
    <div
      v-if="!readonly && toolbar !== 'none'"
      class="border-border bg-card flex flex-wrap items-center gap-0.5 border-b px-1 py-1"
    >
      <template v-for="(group, gi) in toolGroups()" :key="gi">
        <span v-if="gi > 0" class="bg-border mx-1 h-4 w-px"></span>
        <Tooltip v-for="tool in group" :key="tool.title" :title="tool.title">
          <Button
            :class="tool.isActive?.() ? 'bg-accent text-accent-foreground' : ''"
            :disabled="!editor"
            size="small"
            type="text"
            @click="tool.run"
          >
            <IconifyIcon :icon="tool.icon" class="size-4" />
          </Button>
        </Tooltip>
      </template>

      <!-- 글자색: 네이티브 색상 선택기. 한 줄짜리 도구 모음에는 넣지 않는다 -->
      <span v-if="toolbar === 'full'" class="bg-border mx-1 h-4 w-px"></span>
      <Tooltip v-if="toolbar === 'full'" title="글자색">
        <label class="hover:bg-accent flex size-6 cursor-pointer items-center justify-center rounded">
          <IconifyIcon class="size-4" icon="lucide:palette" />
          <input
            class="absolute size-0 opacity-0"
            type="color"
            @input="onPickColor"
          />
        </label>
      </Tooltip>

      <span class="flex-1"></span>
      <span v-if="uploading > 0" class="text-muted-foreground pr-2 text-xs">
        이미지 올리는 중… ({{ uploading }})
      </span>
    </div>

    <Spin :spinning="uploading > 0" size="small">
      <!--
        tiptap 이 기본으로 주는 떠 있는 UI 둘.
        버튼 모양은 우리가 그리고, 언제 어디에 뜰지는 tiptap 이 계산한다.
      -->
      <template v-if="editor && !readonly">
        <!-- 글자를 선택하면 그 위에 뜨는 서식 메뉴 -->
        <BubbleMenu
          :editor="editor"
          :should-show="({ editor: e }) => !e.state.selection.empty && !inTable()"
          plugin-key="formatBubble"
          class="border-border bg-popover flex items-center gap-0.5 rounded-md border p-1 shadow-md"
        >
          <Button
            v-for="tool in [...formatTools(), insertTools()[0]!]"
            :key="tool.title"
            :class="tool.isActive?.() ? 'bg-accent text-accent-foreground' : ''"
            :title="tool.title"
            size="small"
            type="text"
            @click="tool.run"
          >
            <IconifyIcon :icon="tool.icon" class="size-4" />
          </Button>
        </BubbleMenu>

        <!-- 표 안에 커서가 있을 때 뜨는 표 조작 메뉴 -->
        <BubbleMenu
          :editor="editor"
          :should-show="() => inTable()"
          plugin-key="tableBubble"
          class="border-border bg-popover flex items-center gap-0.5 rounded-md border p-1 shadow-md"
        >
          <Button
            v-for="tool in tableTools()"
            :key="tool.title"
            :title="tool.title"
            size="small"
            type="text"
            @click="tool.run"
          >
            <IconifyIcon :icon="tool.icon" class="size-4" />
          </Button>
        </BubbleMenu>
      </template>

      <EditorContent
        class="jsini-rich-editor"
        :editor="editor"
        :style="{ minHeight: `${minHeight}px` }"
      />
    </Spin>

    <!-- 아래 줄: 글자수와 안내 -->
    <div
      v-if="!readonly && toolbar === 'full'"
      class="border-border bg-card text-muted-foreground flex items-center gap-3 border-t px-2 py-0.5 text-[11px]"
    >
      <span>글자를 선택하면 서식 메뉴가 떠오릅니다.</span>
      <span class="flex-1"></span>
      <span>{{ charCount() }} 자</span>
    </div>

    <!-- 도구 모음의 이미지 버튼이 대신 눌러 주는 입력. 화면에는 보이지 않아야 한다.
         tailwind 의 hidden 클래스가 ant 의 폼 스타일에 밀려 노출되던 자리라 인라인으로 못 박는다. -->
    <input
      ref="fileInputRef"
      accept="image/*"
      multiple
      style="display: none"
      type="file"
      @change="onPickedImage"
    />
  </div>
</template>

<style scoped>
.jsini-rich-editor :deep(.jsini-rich-editor-content) {
  min-height: inherit;
  padding: 0.5rem 0.75rem;
  font-size: 0.875rem;
  outline: none;
}

.jsini-rich-editor :deep(img) {
  max-width: 100%;
  height: auto;
  border-radius: 0.25rem;
}

/* 선택한 이미지가 어떤 것인지 보이게 한다 */
.jsini-rich-editor :deep(img.ProseMirror-selectednode) {
  outline: 2px solid hsl(var(--primary));
}

.jsini-rich-editor :deep(h2) {
  margin: 0.5rem 0;
  font-size: 1.125rem;
  font-weight: 600;
}

.jsini-rich-editor :deep(h3) {
  margin: 0.5rem 0;
  font-size: 1rem;
  font-weight: 600;
}

.jsini-rich-editor :deep(ul),
.jsini-rich-editor :deep(ol) {
  padding-left: 1.25rem;
  list-style: revert;
}

/* 체크 목록 — 기본 글머리표를 없애고 체크박스를 앞에 둔다 */
.jsini-rich-editor :deep(ul[data-type='taskList']) {
  padding-left: 0.25rem;
  list-style: none;
}

.jsini-rich-editor :deep(ul[data-type='taskList'] li) {
  display: flex;
  gap: 0.5rem;
  align-items: flex-start;
}

.jsini-rich-editor :deep(ul[data-type='taskList'] li > label) {
  margin-top: 0.15rem;
}

.jsini-rich-editor :deep(blockquote) {
  padding-left: 0.75rem;
  border-left: 3px solid hsl(var(--border));
  color: hsl(var(--muted-foreground));
}

.jsini-rich-editor :deep(pre) {
  padding: 0.5rem 0.75rem;
  overflow-x: auto;
  font-family: ui-monospace, monospace;
  font-size: 0.8125rem;
  background-color: hsl(var(--muted));
  border-radius: 0.25rem;
}

.jsini-rich-editor :deep(a) {
  color: hsl(var(--primary));
  text-decoration: underline;
}

.jsini-rich-editor :deep(hr) {
  margin: 0.75rem 0;
  border-top: 1px solid hsl(var(--border));
}

/* 표 — 테두리가 없으면 편집 중에 칸을 알아볼 수 없다 */
.jsini-rich-editor :deep(table) {
  width: 100%;
  margin: 0.5rem 0;
  table-layout: fixed;
  border-collapse: collapse;
}

.jsini-rich-editor :deep(table td),
.jsini-rich-editor :deep(table th) {
  position: relative;
  padding: 0.25rem 0.5rem;
  border: 1px solid hsl(var(--border));
}

.jsini-rich-editor :deep(table th) {
  font-weight: 600;
  text-align: left;
  background-color: hsl(var(--muted));
}

/* 선택한 셀 */
.jsini-rich-editor :deep(.selectedCell::after) {
  position: absolute;
  inset: 0;
  z-index: 2;
  pointer-events: none;
  content: '';
  background: hsl(var(--primary) / 15%);
}

/* 열 너비 조절 손잡이 */
.jsini-rich-editor :deep(.column-resize-handle) {
  position: absolute;
  top: 0;
  right: -2px;
  bottom: 0;
  width: 4px;
  cursor: col-resize;
  background-color: hsl(var(--primary));
}

/* 비어 있을 때 안내 문구 (Placeholder 확장이 넣는 속성) */
.jsini-rich-editor :deep(p.is-editor-empty:first-child::before) {
  float: left;
  height: 0;
  color: hsl(var(--muted-foreground));
  pointer-events: none;
  content: attr(data-placeholder);
}
</style>
