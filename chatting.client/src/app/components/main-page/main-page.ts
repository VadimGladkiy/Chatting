import { Component, OnInit, OnDestroy, ViewChild, ChangeDetectorRef } from '@angular/core';
import { NgIf } from '@angular/common';
import { TreeWrapperComponent } from '../tree-wrapper/tree-wrapper';
import { CommentFormComponent } from '../comment-form/comment-form';
import { ModalTriggerService, ModalRequest } from '../../services/modal-trigger-service';
import { Comment, EditedComment, FileAttachment } from '../../models/comment';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-main-page',
  standalone: true,
  imports: [TreeWrapperComponent, NgIf, CommentFormComponent, FormsModule],
  templateUrl: './main-page.html',
  styleUrls: ['./main-page.css']
})
export class MainPageComponent implements OnInit, OnDestroy {
  private modalRequest:  ModalRequest | null = null;
  private editedComment: EditedComment | null = null;
  previewOpen = false;
  private previewTitle = '';
  private previewContent = '';
  private textFileLoading = false;

  @ViewChild('childRef') threadList!: TreeWrapperComponent;

  constructor(private cdr: ChangeDetectorRef, private modalTriggerService: ModalTriggerService){}
  
  ngOnInit(){
    this.modalTriggerService.openModal$.subscribe((request) => {
      this.editedComment = request.requestData;
      this.attachFormHandlers(this.editedComment);
      this.modalRequest = request; // opens the modal
      this.cdr.detectChanges();
      this.modalRequest.responseChannel.subscribe({
      complete: () => {
        this.modalRequest = null;
        if(this.editedComment){
          this.detachFormHandlers(this.editedComment);
        }
        this.editedComment = null;
        this.cdr.detectChanges();
      }
      });
    });
  }

  ngOnDestroy(){
    
  }

  // expose handlers that the CommentFormComponent can invoke via the model
  private attachFormHandlers(comment: EditedComment) {
    // attach a function reference to be called from the form file input
    (comment as any).textFileAttached = (e: Event) => this.onTextFileSelected(e);
    (comment as any).textFileRemove = () => this.removeTextFile();
    // attach optional preview callback so the form can ask the page to open the lightbox
    (comment as any).textFilePreview = (attachment: FileAttachment) => this.openPreview(attachment);
  }

  // expose handlers that the CommentFormComponent can invoke via the model
  private detachFormHandlers(comment: EditedComment) {
    // attach a function reference to be called from the form file input
    (comment as any).textFileHandler = null;
    (comment as any).textFileRemove = null;
    // attach optional preview callback so the form can ask the page to open the lightbox
    (comment as any).textFilePreview = null;
  }

  // Handle text file selection (only .txt and <= 100kb allowed)
  onTextFileSelected(event: Event): void {

    if(!this.editedComment){
      return;
    }

    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) {
        this.editedComment.textFileName = '';
        this.editedComment.textFileContent = '';
        this.editedComment.textFileError = '';
      return;
    }

    const file = input.files[0];
    const allowed = /\.txt$/i;
    const maxBytes = 100 * 1024; // 100 KB

    if (!allowed.test(file.name)) {
        this.editedComment.textFileError = 'Only .txt files are allowed.';
        this.editedComment.textFileName = '';
        this.editedComment.textFileContent = '';
      return;
    }

    if (file.size > maxBytes) {
      this.editedComment.textFileError = 'File must be 100 KB or smaller.';
      this.editedComment.textFileName = '';
      this.editedComment.textFileContent = '';
      return;
    }

    this.editedComment.textFileError = '';
    this.textFileLoading = true;
    const reader = new FileReader();
    reader.onload = (e: any) => {
      if(!this.editedComment){
        return;
      }
      this.editedComment.textFileName = file.name;
      this.editedComment.textFileContent = e.target.result as string;
      this.textFileLoading = false;
      this.cdr.detectChanges();
      this.modalTriggerService.notifyModelChanged();
    };
    reader.onerror = () => {
      if(!this.editedComment){
      return;
      }
      this.editedComment.textFileError = 'Failed to read file.';
      this.textFileLoading = false;
      this.cdr.detectChanges();
    };
    reader.readAsText(file);
  }

  removeTextFile(): void {
    if(!this.editedComment){
      return;
    }
    this.editedComment.textFileName = '';
    this.editedComment.textFileContent = '';
    this.editedComment.textFileError = '';
    // reset native input field if needed - leave to DOM
  }

  openPreview(item: FileAttachment): void {
    if (!item || !item.textFileName){
      return;
    }
    this.previewTitle = item.textFileName;
    this.previewContent = item.textFileContent ?? '';
    this.previewOpen = true;
    this.cdr.detectChanges();
  }

  closePreview(): void {
    this.previewOpen = false;
    this.previewTitle = '';
    this.previewContent = '';
    this.cdr.detectChanges();
  }

  // Download attached text file content for an item (if present)
  downloadTextFile(item: Comment & { textFileName?: string; textFileContent?: string }): void {
    if (!item || !item.textFileName) return;
    // open preview using lightbox overlay
    this.previewTitle = item.textFileName;
    this.previewContent = item.textFileContent ?? '';
    this.previewOpen = true;
    this.cdr.detectChanges();
  }
}
