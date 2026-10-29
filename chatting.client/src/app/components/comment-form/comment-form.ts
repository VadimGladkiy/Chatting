import { Component, Input, ChangeDetectorRef, ViewChild, ElementRef, AfterViewInit, inject, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { finalize } from 'rxjs/operators';
import { v4 as uuidv4 } from 'uuid';
import { PhotoUploaderComponent } from '../photo-uploader/photo-uploader';
import { EditedComment, FileAttachment } from '../../models/comment';
import { CommentApiService } from '../../services/comment-api-service';
import { CommentWsService } from '../../services/comment-ws-service';
import { ModalTriggerService } from '../../services/modal-trigger-service';

@Component({
  selector: 'app-comment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, PhotoUploaderComponent],
  providers: [DatePipe],
  templateUrl: './comment-form.html',
  styleUrls: ['./comment-form.css']
})
export class CommentFormComponent implements OnInit, AfterViewInit {

  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;

  // inputs
  @Input() model!: EditedComment;
  @Input() responseChannel!: Subject<EditedComment | null>;

  // Local state
  localCaptchaQuestion: string | null = null;
  localCaptchaImage: string | null = null;
  localCaptchaId: string | null = null;
  localCaptchaAnswer: string | null = null;
  localCaptchaLoading = false;
  localCaptchaError: string | null = null;
  fileOpening = false;

  // Flag to track if file picker is active to prevent accidental cancel
  private filePickerActive = false;
  private datePipe = inject(DatePipe);

  constructor(
    private modalTriggerService: ModalTriggerService,
    private commentApiService: CommentApiService,
    private commentWsService: CommentWsService,
    private cdr: ChangeDetectorRef){}

  ngOnInit(): void {

    this.modalTriggerService.notifyModel$.subscribe(() => {
      this.cdr.detectChanges();
    });

    this.loadCaptchaInternal();
  }

  ngAfterViewInit(): void {
    // Attach cancel event listener to prevent form close when file picker cancels
    if (this.fileInput && this.fileInput.nativeElement) {
      const input = this.fileInput.nativeElement;

      // Listen for native cancel event
      input.addEventListener('cancel', (event: Event) => {
        console.log('Native cancel event fired from file input');
        event.stopPropagation();
        this.filePickerActive = false;
      });

      // Track when file picker opens
      input.addEventListener('click', () => {
        this.filePickerActive = true;
      });
    }
  }

  insertTag(tag: 'strong' | 'i' | 'code' | 'a', textarea: HTMLTextAreaElement): void {
    const text = textarea.value;
    const start = textarea.selectionStart;
    const end = textarea.selectionEnd;
    const selectedText = text.slice(start, end);
    const openingTag = tag === 'a' ? '<a href="">' : `<${tag}>`;
    const closingTag = `</${tag}>`;
    const replacement = `${openingTag}${selectedText}${closingTag}`;

    textarea.setRangeText(replacement, start, end, 'end');
    textarea.dispatchEvent(new Event('input', { bubbles: true }));
    this.cdr.detectChanges();
    textarea.focus();

    const cursorPosition = tag === 'a'
      ? start + '<a href="'.length
      : start + openingTag.length + selectedText.length;
    textarea.setSelectionRange(cursorPosition, cursorPosition);
  }

  // Validate allowed HTML tags only
  private validateTags(text: string): string | null {
    if (!text) return null;

    // Allowed tags patterns
    const allowedTags = ['strong', 'i', 'code', 'a'];

    // Check for invalid angle bracket tags: <...>
    const angleBracketPattern = /<([^>]+)>/g;
    const tagStack: Array<{ name: string; hasText: boolean }> = [];
    let textPosition = 0;
    let match;

    while ((match = angleBracketPattern.exec(text)) !== null) {
      if (/\S/.test(text.slice(textPosition, match.index))) {
        tagStack.forEach(tag => tag.hasText = true);
      }
      textPosition = angleBracketPattern.lastIndex;

      let tagContent = match[1].trim().toLowerCase();

      // Identify closing tags, then normalize their names for allow-list checking.
      const isClosingTag = tagContent.startsWith('/');
      if (isClosingTag) {
        tagContent = tagContent.substring(1).trim();
      }

      // Extract tag name (first word before space or />)
      const tagName = tagContent.split(/[\s/>]/)[0];

      // Skip empty tag names (shouldn't happen, but be safe)
      if (!tagName) continue;

      // Check if it's an allowed tag
      if (!allowedTags.includes(tagName)) {
        return `Invalid tag: <${tagName}>. Allowed tags: <strong>, <i>, <code>, <a href="...">`;
      }

      if (!isClosingTag && tagName === 'a') {
        const hrefMatch = tagContent.match(/(?:^|\s)href\s*=\s*(?:"([^"]*)"|'([^']*)'|([^\s/>]+))/i);
        const href = hrefMatch?.[1] ?? hrefMatch?.[2] ?? hrefMatch?.[3];
        if (!href || !this.isValidHref(href)) {
          return 'The <a> tag must have a valid absolute http or https href.';
        }
      }

      if (isClosingTag) {
        const openingTag = tagStack.pop();
        if (!openingTag || openingTag.name !== tagName) {
          return `Closing tag </${tagName}> does not match its opening tag.`;
        }
        if (!openingTag.hasText) {
          return `The <${tagName}> tag must contain text.`;
        }
      } else {
        tagStack.push({ name: tagName, hasText: false });
      }
    }

    if (/\S/.test(text.slice(textPosition))) {
      tagStack.forEach(tag => tag.hasText = true);
    }

    if (tagStack.length > 0) {
      return `Missing closing tag for <${tagStack[tagStack.length - 1].name}>.`;
    }

    return null;
  }

  private isValidHref(href: string): boolean {
    try {
      const url = new URL(href);
      return (url.protocol === 'http:' || url.protocol === 'https:') && !!url.hostname;
    } catch {
      return false;
    }
  }

  onLoadCaptcha(): void {
    this.localCaptchaError = null;
    this.loadCaptchaInternal();
  }

  async onSubmit(event: Event, f: any): Promise<void>{

    // prevent default form submission behavior 
    event.preventDefault();
    event.stopPropagation();

    if (f.invalid){ return };

    if(this.model.text)
    {
      // Validate tags in the comment text
      const tagError = this.validateTags(this.model.text);
      if (tagError) {
        this.model.textError = tagError;
        return;
      }
    }

    // client-side tag checks as you already have...
    this.localCaptchaError = null;

    try {

      if(!this.localCaptchaId || !this.localCaptchaAnswer){
        return;
      }

      const res: any = await this.commentWsService.validateCaptcha(this.localCaptchaId, this.localCaptchaAnswer);
      if (!res.ok) {
        this.localCaptchaError = 'Captcha is incorrect. Please try again.';
        this.loadCaptchaInternal(); // reload new captcha from server
        this.cdr.detectChanges();
        return;
      }
      
      // here translate a payload into a pending comment
      const now: Date = new Date();
      const d: string = this.datePipe.transform(now, 'yyyy-MM-dd HH:mm:ss') ?? 'error converting date';
      const tempId = uuidv4();

      this.model.id = tempId,
      this.model.date = d;
      this.model.isPending = true;

      // push a pending comment to the modal invoker
      this.responseChannel.next(this.model);
      this.responseChannel.complete();

      // Clear any previous error
      // this.resetFormModel();
    }
    catch (err) {
      console.error(err);
      this.localCaptchaError = 'Failed to validate captcha';
    } finally {
      // this.posting = false;
    }
  }

  onCaptchaInput(): void {
    this.localCaptchaError = null;
  }

  onTextFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;

    if (!input.files || input.files.length === 0) {
      if (this.model) {
        this.model.textFileName = '';
        this.model.textFileContent = '';
        this.model.textFileError = '';
      }
      this.filePickerActive = false;
      return;
    }

    event.stopPropagation();

    // Reset and replay animation
    this.fileOpening = false;
    this.cdr.detectChanges();

    setTimeout(() => {
      this.fileOpening = true;
      this.cdr.detectChanges();
    }, 50);

    // Read file after animation starts
    setTimeout(() => {
      if (this.model && (this.model as any).textFileAttached) {
        (this.model as any).textFileAttached(event);
      }
      else {
        const file = input.files![0];
        const reader = new FileReader();

        reader.onload = (e: any) => {
          if (this.model) {
            this.model.textFileName = file.name;
            this.model.textFileContent = e.target.result as string;
            this.model.textFileError = '';
          }
        };

        reader.onerror = () => {
          if (this.model) {
            this.model.textFileError = 'Failed to read file.';
          }
        };

        reader.readAsText(file);
      }
      this.cdr.detectChanges();
    }, 120);

    this.filePickerActive = false;
  }

  onFileAnimationEnd(e: AnimationEvent): void {
    if (e && e.animationName === 'file-opening') {
      this.fileOpening = false;
      this.cdr.detectChanges();
    }
  }

  onRemoveFile(fileInput: HTMLInputElement): void {
    if (this.model) {
      this.model.textFileName = '';
      this.model.textFileContent = '';
      this.model.textFileError = '';
    }
    fileInput.value = '';
    this.fileOpening = false;
    this.cdr.detectChanges();

    if((this.model as any).textFileRemove){
      (this.model as any).textFileRemove();
    }
  }

  onFilePreview(fileInput: HTMLInputElement){
    if((this.model as any).textFilePreview){
      const file = {
        textFileName: this.model.textFileName,
        textFileContent: this.model.textFileContent
      } as FileAttachment;
      (this.model as any).textFilePreview(file);
    }
  }

  onCancel(): void {
    // PREVENT CANCEL if file picker was recently active
    if (this.filePickerActive) {
      console.log('⚠️ BLOCKING Cancel - file picker still active!');
      return;
    }

    // Clear all form fields before closing
    this.resetFormModel();
    this.cdr.detectChanges();
    this.responseChannel.next(null);
    this.responseChannel.complete();
  }

  private escapeHtml(str: string): string {
    return str.replace(/[&<>"']/g, (m) => {
      return ({
        '&': '&amp;',
        '<': '&lt;',
        '>': '&gt;',
        '"': '&quot;',
        "'": '&#39;'
      } as Record<string, string>)[m] ?? m;
    });
  }

  private loadCaptchaInternal(): void {
    this.localCaptchaLoading = true;

    this.commentApiService.getCaptcha().pipe(
      finalize(() => {
        this.localCaptchaLoading = false;
        this.cdr.detectChanges();
      })
    ).subscribe({
      next: (resp) => {
        this.localCaptchaId = resp.id;
        this.localCaptchaImage = resp.imageData ?? null;
        this.localCaptchaQuestion = resp.imageData ? null : resp.question ?? null;
        this.cdr.detectChanges();
      },
      error: () => {
        this.localCaptchaQuestion = null;
        this.localCaptchaImage = null;
        this.localCaptchaError = 'Failed to load captcha.';
        this.cdr.detectChanges();
      }
    });
  }

  private resetFormModel(): void {
    if (!this.model) return;
    this.model.userName = '';
    this.model.userEmail = '';
    this.model.homePageUrl = '';
    this.model.text = '';
    this.model.textError = '';
    this.model.imageUrl = '';
    this.model.textFileName = '';
    this.model.textFileContent = '';
    this.model.textFileError = '';

    this.fileOpening = false;
    this.localCaptchaError = null;
  }
}
