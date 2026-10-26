import { Component, Input, Output, EventEmitter, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { Comment, EditedComment, FileAttachment } from '../../models/comment';

@Component({
  selector: 'app-list-item',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './list-item.html',
  styleUrls: ['./list-item.css']
})
export class ListItemComponent implements OnInit, OnDestroy{
  @Input() comment!: Comment;
  @Output() replyClicked = new EventEmitter<Comment>();
  @Output() denyPendingClicked = new EventEmitter<any>();
  @Output() confirmPendingClicked = new EventEmitter<EditedComment>();
  @Output() filePreviewClicked = new EventEmitter<FileAttachment>();

  constructor(private cdr: ChangeDetectorRef,
    private sanitizer: DomSanitizer){}

  ngOnInit(){

  }

  ngOnDestroy(){
    
  }

  onConfirmPendingClick(event: Event): void {
    event.stopPropagation();
    this.confirmPendingClicked.emit(this.comment as EditedComment);
  }

  onDenyPendingClick(event: Event): void {
    event.stopPropagation();
    this.denyPendingClicked.emit(this.comment);
  }

  onReplyClick(event: Event): void {
    event.stopPropagation();
    this.replyClicked.emit(this.comment);
  }

  onFilePreviewClick($event: Event, file: any): void{
    const newFile: FileAttachment = {
      textFileName: file.textFileName,
      textFileContent: file.textFileContent,
    };
    this.filePreviewClicked.emit(newFile);
  }

  // Render user-supplied lightweight markup ([i], [strong], [code], [a]) to safe HTML
  private renderText(raw: string | undefined | null): SafeHtml {
    const s = raw ?? '';
    const html = this.markupToHtml(s);
    return this.sanitizer.bypassSecurityTrustHtml(html);
  }

  // Convert simple BBCode-like tags to escaped HTML. This function always escapes input
  // first to prevent injection, then replaces recognized tags.
  private markupToHtml(input: string): string {
    // If the user submitted raw HTML (<...>), sanitize and allow a small whitelist of tags.
    // Otherwise, treat input as lightweight BBCode and convert known tags.
    if (input.includes('<')) {
      return this.sanitizeAllowedHtml(input);
    }

    // escape
    const esc = (str: string) => str.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

    let out = esc(input);

    // [code] blocks: treat specially so inner content remains escaped but preserved formatting
    out = out.replace(/\[code\]([\s\S]*?)\[\/code\]/gi, (_m, p1) => {
      return '<pre><code>' + p1 + '</code></pre>';
    });

    // [strong] -> <strong>
    out = out.replace(/\[strong\]([\s\S]*?)\[\/strong\]/gi, '<strong>$1</strong>');
    // [i] -> <em>
    out = out.replace(/\[i\]([\s\S]*?)\[\/i\]/gi, '<em>$1</em>');

    // [a=URL]text[/a] or [a]URL[/a]
    out = out.replace(/\[a=(https?:\/\/[^\]]+)\]([\s\S]*?)\[\/a\]/gi, (_m, href, txt) => {
      const safe = href.replace(/"/g, '%22');
      return '<a href="' + safe + '" target="_blank" rel="noopener noreferrer">' + txt + '</a>';
    });
    // [a]URL[/a] -> <a href="URL">URL</a>
    out = out.replace(/\[a\]((?:https?:\/\/)[^\[]*?)\[\/a\]/gi, (_m, href) => {
      const safe = href.replace(/"/g, '%22');
      return '<a href="' + safe + '" target="_blank" rel="noopener noreferrer">' + href + '</a>';
    });

    // convert line breaks to <br>
    out = out.replace(/\r?\n/g, '<br/>');

    return out;
  }

  // Sanitize HTML input and allow only a small whitelist of tags and attributes.
  private sanitizeAllowedHtml(input: string): string {
    const parser = new DOMParser();
    const doc = parser.parseFromString(input, 'text/html');
    const allowed = new Set(['strong', 'b', 'em', 'i', 'pre', 'code', 'a', 'br']);

    const escapeText = (s: string) => s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

    function walk(node: Node): string {
      if (node.nodeType === Node.TEXT_NODE) {
        return escapeText(node.textContent ?? '');
      }
      if (node.nodeType === Node.ELEMENT_NODE) {
        const el = node as HTMLElement;
        const tag = el.tagName.toLowerCase();
        if (!allowed.has(tag)) {
          // unwrap unknown tags but keep their children
          return Array.from(el.childNodes).map(walk).join('');
        }

        if (tag === 'a') {
          const href = el.getAttribute('href') ?? '';
          const safeHref = (href.startsWith('http://') || href.startsWith('https://')) ? href.replace(/"/g, '%22') : '';
          const inner = Array.from(el.childNodes).map(walk).join('');
          if (!safeHref) return inner;
          return '<a href="' + safeHref + '" target="_blank" rel="noopener noreferrer">' + inner + '</a>';
        }

        if (tag === 'br') return '<br/>';

        const inner = Array.from(el.childNodes).map(walk).join('');
        return '<' + tag + '>' + inner + '</' + tag + '>';
      }
      return '';
    }

    return Array.from(doc.body.childNodes).map(walk).join('');
  }
}
