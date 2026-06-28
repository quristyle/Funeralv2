declare module 'tui-image-editor' {
  class ImageEditor {
    constructor(wrapper: string | Element, options: any);
    loadImageFromURL(url: string, name: string): Promise<any>;
    toDataURL(options?: any): string;
    clearUndoStack(): void;
    clearRedoStack(): void;
    destroy(): void;
    startDrawingMode(mode: string): any;
    setCropzoneAspectRatio(ratio: number): void;
    // 필요한 메소드가 더 있다면 정의
  }
  export default ImageEditor;
}
