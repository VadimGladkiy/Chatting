import { Injectable } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import { EditedComment } from '../models/comment';

export interface ModalRequest {
  requestData: EditedComment;
  responseChannel: Subject<EditedComment | null>; 
}

@Injectable({
  providedIn: 'root'
})
export class ModalTriggerService {
  private openModalSource = new Subject<ModalRequest>();
  public openModal$: Observable<ModalRequest> = this.openModalSource.asObservable();

  public triggerOpen(requestData: EditedComment, responseChannel: Subject<EditedComment | null>): void {
    this.openModalSource.next({ requestData, responseChannel });
  }

  private notifierSource = new Subject<any>();
  public notifyModel$: Observable<any> = this.notifierSource.asObservable();

  public notifyModelChanged(){
    this.notifierSource.next({});
  }
}
