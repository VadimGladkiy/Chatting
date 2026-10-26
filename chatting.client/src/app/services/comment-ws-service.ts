// comment-ws-service.ts
import { Injectable } from '@angular/core';
import { Subject, Observable } from 'rxjs';
import { webSocket, WebSocketSubject } from 'rxjs/webSocket';
import { CommentNotification } from '../models/comment';

@Injectable({ providedIn: 'root' })
export class CommentWsService {
  // RxJS WebSocket сам управляет очередью отправки (буферизацией до статуса OPEN)
  private socket$!: WebSocketSubject<any>;

  // Через этот поток компоненты будут получать сообщения
  public messages$: Observable<any>;
  private pending = new Map<string, (value: any) => void>();
  private pendingReject = new Map<string, (err: any) => void>();
  public childCreated$ = new Subject<CommentNotification>();

  constructor() {
    this.socket$ = webSocket({
      url: 'ws://localhost:5111/ws/comments',
      deserializer: (msg) => msg.data,
      
      // 2. Настраиваем хук открытия сокета
      openObserver: {
        next: (event: Event) => {
          console.log('Соединение с .NET сервером установлено!');
        }
      },
      
      // Опционально: хук закрытия сокета
      closeObserver: {
        next: (event: CloseEvent) => {
          console.log('Соединение потеряно.', event.reason);
        }
      }
    });

    this.messages$ = this.socket$.asObservable();

    // Слушаем входящие сообщения от .NET Core бэкенда
    this.socket$.subscribe({
      next: (rawData: string) => this.handleMessage(rawData),
      error: (err) => console.error('Ошибка сокета:', err)
    });
  }

 private handleMessage(data: string) {
    let msg: any;
    try { msg = JSON.parse(data); } catch { return; }
    if (!msg || !msg.type) return;

    if (msg.type === 'validateCaptchaResult' && msg.requestId) {
      const resolve = this.pending.get(msg.requestId);
      const reject = this.pendingReject.get(msg.requestId);

      if (resolve) {
        resolve(msg);
        this.pending.delete(msg.requestId);
        this.pendingReject.delete(msg.requestId);
      } else if (reject) {
        reject(new Error('No handler found for this request'));
        this.pending.delete(msg.requestId);
        this.pendingReject.delete(msg.requestId);
      }
    }

    if (msg.type === 'commentCreated' && msg.requestId) {
      const resolve = this.pending.get(msg.requestId);
      const reject = this.pendingReject.get(msg.requestId);

      if (resolve) {
        resolve(msg);
        this.pending.delete(msg.requestId);
        this.pendingReject.delete(msg.requestId);
      } else if (reject) {
        reject(new Error('No handler found for this request'));
        this.pending.delete(msg.requestId);
        this.pendingReject.delete(msg.requestId);
      }
    }

    // Server may push commentCreated notifications for actions not initiated via WS request
    // (for example when a comment was created via HTTP POST). Handle those too.
    if (msg.type === 'commentCreated' && !msg.requestId) {
      // notify subscribers
      if (msg.model) {
        this.childCreated$.next(msg.model);
      } else {
        // fallback: emit raw message on messages$ stream by pushing to socket observable is not possible here,
        // so just log for debugging.
        console.debug('Received commentCreated push without requestId but missing parent/child', msg);
      }
    }

    // handle other server pushes as needed...
  }

 private sendRequest(payload: any, timeoutMs = 10000): Promise<any> {
    const requestId = (crypto && (crypto as any).randomUUID) ? (crypto as any).randomUUID() : Math.random().toString(36).slice(2);
    payload.requestId = requestId;
    return new Promise((resolve, reject) => {
      this.pending.set(requestId, resolve);
      this.pendingReject.set(requestId, reject);
      try {
        this.socket$!.next(payload);
      } catch (err) {
        this.pending.delete(requestId);
        this.pendingReject.delete(requestId);
        return reject(err);
      }
      const t = setTimeout(() => {
        if (this.pending.has(requestId)) {
          this.pending.delete(requestId);
          this.pendingReject.delete(requestId);
          reject(new Error('Timeout'));
        }
      }, timeoutMs);
      // resolve/reject cleanup will remove pending; clear timeout in handlers if desired
    });
  }

  // call this to validate captcha; resolves to { ok: boolean, message }
  validateCaptcha(captchaId: string, answer: string): Promise<{ ok: boolean; message?: string }> {

    let toSend: any = {
      type : 'validateCaptcha',
      captchaId : captchaId,
      answer : answer 
    }

    return this.sendRequest(toSend);
  }
}
