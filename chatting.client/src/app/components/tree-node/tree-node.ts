import { Component, Input, OnInit, OnDestroy, ChangeDetectorRef, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subscription, Subject, firstValueFrom } from 'rxjs';
import { TreeNode, Comment, PageInfo, FileAttachment, EditedComment, PagedResponse } from '../../models/comment';
import { CommentApiService } from '../../services/comment-api-service';
import { ModalTriggerService } from '../../services/modal-trigger-service';
import { TreeNodeRegisterService } from '../../services/tree-node-register-service';
import { ListItemComponent } from '../list-item/list-item';
import { ScrollingModule } from '@angular/cdk/scrolling';
import { ConfigService } from '../../services/config-service';

@Component({
  selector: 'app-tree-node',
  standalone: true,
  imports: [CommonModule, ListItemComponent, ScrollingModule],
  templateUrl: './tree-node.html',
  styleUrls: ['./tree-node.css']
})
export class TreeNodeComponent implements OnInit, OnDestroy {
  @Input() model!: TreeNode;
  @Input() depth: number = 0;
  @Input() pageSize: number = 25;
  @Output() denyPending = new EventEmitter<TreeNode>();
  @Output() openFilePreview = new EventEmitter<FileAttachment>();

  children: TreeNode[] = [];
  childrenTotal: number = 0;
  currentPage: number = 1;
  totalPages: number = 1;
  isLoading = false;
  expanded = false;

  // communication
  popupSubscription: Subscription | null = null;
  responseChannel: Subject<EditedComment | null> = new Subject<EditedComment | null>();

  constructor(private treeNodeRegisterService: TreeNodeRegisterService,
    private modalTriggerService: ModalTriggerService,
    private commentApiService: CommentApiService,
    private cdr: ChangeDetectorRef,
    private configService: ConfigService){}

  ngOnInit(): void {
    this.pageSize = this.configService.pageSize;

    if(!this.isPending()){
     this.treeNodeRegisterService.registerTreeNode(this.model.comment.id, (x,y) => this.onChildCreated(x,y));
    }
  }

  private isPending(){
    return 'isPending' in this.model.comment && this.model.comment.isPending;
  }

  toggle(event?: Event) {
    if (event) event.stopPropagation();
    if (!this.expanded) {
      this.expanded = true;
      if (this.children.length == 0) {
        this.loadChildren(1);
      }
    } else {
      this.expanded = false;
    }
  }

  ngOnDestroy(){
    this.clearCommunication();
    this.treeNodeRegisterService.unregisterTreeNode(this.model.comment.id);
  }

  loadChildren(page: number) {
    this.isLoading = true;
    this.commentApiService.getItems(page, this.pageSize, this.model.comment.id, 'date', 'desc').subscribe({
      next: (resp) => {
        this.insertChildren(resp, page);
      },
      error: (err) => {
        console.error('Error loading children for', this.model.comment.id, err);
        this.isLoading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private insertChildren(response: PagedResponse, pageNumber: number){

    this.children = response.comments?.map((x): TreeNode =>
       { return { nodeId: crypto.randomUUID(), comment: x, children: [], isLoading: false };
    }) ?? [];

    this.childrenTotal = response.totalItems ?? (this.children ? this.children.length : 0);
    this.currentPage = response.currentPage ?? pageNumber;
    this.calcTotalPages();
    this.isLoading = false;
    this.cdr.detectChanges();
  }

  private onChildCreated(item: Comment, aboutPage: PageInfo){

    let insert = false;

    if(this.currentPage == aboutPage.replyDescPage){
      insert = true;
    }

    if(insert){
      let node = { nodeId: crypto.randomUUID(), comment: item, children: [], isLoading: false } as TreeNode;
      this.children.unshift(node);

      if(this.children.length > this.pageSize){
        this.children = this.children.slice(0, this.pageSize);
      }

      this.childrenTotal++;
      this.calcTotalPages();
      this.isLoading = false;
      this.cdr.detectChanges();
    }
  }

  private calcTotalPages(){
    this.totalPages = Math.ceil((this.childrenTotal ?? 0) / this.pageSize);
  }

  private async loadChildrenAsync(){
    try {
      this.isLoading = true;
      const request = this.commentApiService.getItems(1, this.pageSize, this.model.comment.id, 'date', 'desc');
      const responseData = await firstValueFrom(request);
      this.insertChildren(responseData, 1);
      this.isLoading = false;
    } catch (error) {
      console.error('Error requesting children async:', error);
    }
  }

  // paging helpers
  canPrev() {
    return this.currentPage > 1;
  }

  canNext() {
    const pages = Math.ceil((this.childrenTotal ?? 0) / this.pageSize);
    return this.currentPage < pages;
  }

  prevPage(event: Event) {
    event.stopPropagation();
    if (this.canPrev()) {
      this.loadChildren(this.currentPage - 1);
    }
  }

  nextPage(event: Event) {
    event.stopPropagation();
    if (this.canNext()) {
      this.loadChildren(this.currentPage + 1);
    }
  }

  // html helper
  trackByFn(index: number, item: TreeNode)
  {
    return item?.nodeId ?? index;
  }

  // event handlers
  async onReplyClicked(param: any){

    if(!this.expanded || !this.children)
    {
      await this.loadChildrenAsync();
      this.expanded = true;
    }

    const newComment: EditedComment = {
      clientId: '',
      id: '',
      userName: '',
      userEmail: '',
      imageUrl: '',
      homePageUrl: '',
      date: '',
      text: '',
      textError: '',
      parentId: this.model.comment.id,
      textFileName: '',
      textFileContent: '',
      textFileError: '',
      isPending: true
    };

    this.responseChannel = new Subject<EditedComment | null>();

    this.popupSubscription = this.responseChannel.subscribe({
      next: (pending) => {
        if (!pending) {
          return;
        }

        const node: TreeNode = { nodeId: crypto.randomUUID(), comment: pending, children: [], isLoading: false };
        this.children = [node, ...this.children ?? []];
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error in popup channel', err);
      },
      complete: () => {
        console.log('Popup channel completed');
        this.clearCommunication();
      }
    })

    // open modal
    this.modalTriggerService.triggerOpen(newComment, this.responseChannel);
  }

  private clearCommunication(): void {
    this.popupSubscription?.unsubscribe();

    if(this.responseChannel){
      //this.responseChannel.next(null);
      this.responseChannel.complete();
    }
  }

  onDenyPendingClicked(param: any){
    this.denyPending.emit(this.model);
  }

  onDenyPendingHandler(pending: TreeNode){
    if(!pending){
      return;
    }

    if(this.children && this.children.some(x => x.comment.id === pending.comment.id)){
      this.children = this.children.filter(x => x.comment.id !== pending.comment.id);
      this.cdr.detectChanges();
    }
  }

  onConfirmPendingClicked(param: EditedComment){

    this.commentApiService.createComment(param).subscribe({
      next: (response) => {

        let comment: Comment = {
          id:response.id,
          date: response.date,
          userName: response.userName,
          userEmail: response.userEmail,
          homePageUrl: response.homePageUrl,
          imageUrl: response.imageUrl,
          text: response.text,
          parentId: response.parentId,
          textFileName: response.textFileName,
          textFileContent: response.textFileContent,
        }

        this.model.comment = comment;
        this.treeNodeRegisterService.registerTreeNode(this.model.comment.id, (x,y) => this.onChildCreated(x,y));
        
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error creation of comment.', err);
        this.cdr.detectChanges();
      }
    });
  }

  onFilePreviewClicked(file: FileAttachment){
    this.openFilePreview.emit(file);
  }
}
