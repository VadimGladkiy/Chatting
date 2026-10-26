// tree-node-register-service.ts
import { Injectable } from '@angular/core';
import { Comment, PageInfo } from '../models/comment';

@Injectable({ providedIn: 'root' })
export class TreeNodeRegisterService {
  private storage = new Map<string, ChildCreatedHandler>();

  public registerTreeNode(key: string, handler: ChildCreatedHandler){
    this.storage.set(key, handler);
  }

  public unregisterTreeNode(key: string){
    this.storage.delete(key);
  }

  public getByKey(key: string): ChildCreatedHandler | undefined{
    return this.storage.get(key);
  }
}

export type ChildCreatedHandler =
  (created: Comment, aboutPage: PageInfo) => void;
