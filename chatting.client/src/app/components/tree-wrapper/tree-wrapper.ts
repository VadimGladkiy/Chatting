import { Component, OnInit, OnDestroy, ChangeDetectorRef, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, Subscription } from 'rxjs';
import { v4 as uuidv4 } from 'uuid';
import { TreeNode, Comment, PageInfo, FileAttachment, EditedComment, CommentNotification } from '../../models/comment';
import { CommentApiService } from '../../services/comment-api-service';
import { TreeNodeComponent } from '../tree-node/tree-node';
import { ModalTriggerService } from '../../services/modal-trigger-service';
import { CommentWsService } from '../../services/comment-ws-service';
import { TreeNodeRegisterService } from '../../services/tree-node-register-service';
import { ConfigService } from '../../services/config-service';

type RootSortField = 'userName' | 'userEmail' | 'date';
type SortDirection = 'asc' | 'desc';

@Component({
  selector: 'app-tree-wrapper',
  standalone: true,
  imports: [CommonModule, TreeNodeComponent],
  templateUrl: './tree-wrapper.html',
  styleUrls: ['./tree-wrapper.css']
})
export class TreeWrapperComponent implements OnInit, OnDestroy {
  @Input() pageSize = 25;
  @Output() openFilePreview: EventEmitter<FileAttachment> = new EventEmitter<FileAttachment>();

  nodes: TreeNode[] = [];
  currentPage = 1;
  totalItems = 0;
  totalPages = 1;
  isLoading = false;
  sortBy: RootSortField = 'date';
  sortDirection: SortDirection = 'desc';
  private rootLoadVersion = 0;

  // communication
  popupSubscription: Subscription | null = null;
  responseChannel: Subject<EditedComment | null> = new Subject<EditedComment | null>();
  clientId!: string;

  constructor(private treeNodeRegisterService: TreeNodeRegisterService,
    private commentApiService: CommentApiService,
    private commentWsService: CommentWsService,
    private modalTriggerService: ModalTriggerService,
    private cdr: ChangeDetectorRef,
    private configService: ConfigService){}

  ngOnInit(): void {
    this.clientId = this.commentApiService.clientId;
    this.pageSize = this.configService.pageSize;

    this.loadRootPage();

    this.treeNodeRegisterService.registerTreeNode('', (x,y) => this.onChildCreated(x,y));

    this.commentWsService.childCreated$.subscribe({
       next: (notification: CommentNotification) => {
         if(notification.clientId === this.clientId){
           return;
         }

         const handler = this.treeNodeRegisterService.getByKey(notification.comment.parentId ?? '');
         if(handler){
           handler(notification.comment, notification.pageInfo);
         }
       },
       error: (err) => {
         console.log(err);
       }
    });
  }

  ngOnDestroy(): void {
    this.clearCommunication();

    this.commentWsService.childCreated$.unsubscribe();

    this.treeNodeRegisterService.unregisterTreeNode('');
  }

  loadRootPage(): void {
    const loadVersion = ++this.rootLoadVersion;
    this.isLoading = true;
    this.commentApiService.getItems(this.currentPage, this.pageSize, null, this.sortBy, this.sortDirection).subscribe({
      next: (resp) => {
        if (loadVersion !== this.rootLoadVersion) {
          return;
        }

        this.nodes = resp.comments?.map((x: Comment): TreeNode => {
          return { nodeId: uuidv4(), comment: x, children: [], isLoading: false };}) ?? [];

        this.totalItems = resp.totalItems ?? 0;
        this.calcTotalPages();

        this.isLoading = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        if (loadVersion !== this.rootLoadVersion) {
          return;
        }
        console.error('Error loading tree root items', err);
        this.isLoading = false;
      }
    });
  }

  sortByColumn(field: RootSortField): void {
    if (this.sortBy === field) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortBy = field;
      this.sortDirection = 'asc';
    }

    this.currentPage = 1;
    this.loadRootPage();
  }

  sortIndicator(field: RootSortField): string {
    if (this.sortBy !== field) {
      return '';
    }

    return this.sortDirection === 'asc' ? '▲' : '▼';
  }

  sortAriaValue(field: RootSortField): string {
    if (this.sortBy !== field) {
      return 'none';
    }

    return this.sortDirection === 'asc' ? 'ascending' : 'descending';
  }

  goToPage(page: number): void {
    if (this.isLoading || page < 1 || page > this.totalPages || page === this.currentPage) {
      return;
    }

    this.currentPage = page;
    this.loadRootPage();
  }

  previousPage(): void {
    this.goToPage(this.currentPage - 1);
  }

  nextPage(): void {
    this.goToPage(this.currentPage + 1);
  }

  private onChildCreated(item: Comment, aboutPage: PageInfo){

    let insert = false;

    if(this.sortBy === 'userName' && this.sortDirection === 'desc' && this.currentPage == aboutPage.threadUserNameDescPage){
      insert = true;
    }

    if(this.sortBy === 'userName' && this.sortDirection === 'asc' && this.currentPage == aboutPage.threadUserNameAscPage){
      insert = true;
    }

    if(this.sortBy === 'userEmail' && this.sortDirection === 'desc' && this.currentPage == aboutPage.threadEmailDescPage){
      insert = true;
    }

    if(this.sortBy === 'userEmail' && this.sortDirection === 'asc' && this.currentPage == aboutPage.threadEmailAscPage){
      insert = true;
    }

    if(this.sortBy === 'date' && this.sortDirection === 'desc' && this.currentPage == aboutPage.threadDateDescPage){
      insert = true;
    }

    if(this.sortBy === 'date' && this.sortDirection === 'asc' && this.currentPage == aboutPage.threadDateAscPage){
      insert = true;
    }

    if(insert){
      let node = { nodeId: uuidv4(), comment: item, children: [], isLoading: false } as TreeNode;
      this.nodes.unshift(node);

      if(this.nodes.length > this.pageSize){
        this.nodes = this.nodes.slice(0, this.pageSize);
      }

      this.totalItems++;
      this.calcTotalPages();
      this.isLoading = false;
      this.cdr.detectChanges();
    }
  }

  private calcTotalPages(){
      this.totalPages = Math.ceil((this.totalItems ?? 0) / this.pageSize);
  }

  // helpers
  trackByComment(_: number, node: TreeNode) {
    return node.comment.id;
  }
  
  // open modal

  onNewThreadClicked(param: any){

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
      parentId: null,
      textFileName: '',
      textFileContent: '',
      textFileError: '',
      isPending: true,
      isLocked: false
    };

    this.responseChannel = new Subject<EditedComment | null>();

    this.popupSubscription = this.responseChannel.subscribe({
      next: (pending) => {
        if (!pending) {
          return;
        }

        const node: TreeNode = { nodeId: uuidv4(), comment: pending, children: [], isLoading: false };
        this.nodes = [node, ...this.nodes];
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error in popup channel', err);
      },
      complete: () => {
        this.clearCommunication();
      }
    });

    // open modal
    this.modalTriggerService.triggerOpen(newComment, this.responseChannel);
  }

  onDenyPendingHandler(pending: TreeNode){

    if(!pending){
      return;
    }

    if(this.nodes && this.nodes.some(x => x.comment.id === pending.comment.id)){
      this.nodes = this.nodes.filter(x => x.comment.id !== pending.comment.id);
      this.cdr.detectChanges();
    }
  }

  onFilePreviewHandler(file: FileAttachment){
    this.openFilePreview.emit(file);
  }

  private clearCommunication(): void {
    this.popupSubscription?.unsubscribe();

    if(this.responseChannel){
      this.responseChannel.complete();
    }
  }
}
