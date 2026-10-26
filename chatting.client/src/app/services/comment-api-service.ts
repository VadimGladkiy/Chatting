import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Comment, EditedComment, PagedResponse } from '../models/comment';

// Interface for the structure of a single ite

@Injectable({
  providedIn: 'root' // Service will be available throughout the application
})
export class CommentApiService {
  // Specify the URL of your API here
  private apiUrl = 'api/Comments';
  public clientId: string = crypto.randomUUID();

  constructor(private http: HttpClient) {}

  // Request a captcha challenge from server
  // getCaptcha will return either { id, question } or { id, imageData } depending on server support
  getCaptcha(): Observable<{ id: string; question?: string; imageData?: string }> {
    return this.http.get<{ id: string; question?: string; imageData?: string }>('api/Captcha');
  }

  // Get from server and translate into client-side shape
  getItems(page: number, size: number, parentId?: string | null, sortBy?: string, sortDirection?: string): Observable<PagedResponse> {
    let params = new HttpParams()
      .set('pageNumber', page.toString())
      .set('pageSize', size.toString());

    if(parentId)
    {
      params = params.set('parentId', parentId);
    }

    if(sortBy)
    {
      params = params.set('sortBy', sortBy);
    }

    if(sortDirection)
    {
      params = params.set('sortDirection', sortDirection);
    }

    return this.http.get<PagedResponse>(this.apiUrl, { params });
  }

  // Create a new comment on the server. Accepts client-side camelCase and maps to server PascalCase.
  createComment(payload: EditedComment) : Observable<Comment> {
    const serverPayload = {
      ClientId: this.clientId,
      UserName: payload.userName,
      UserEmail: payload.userEmail,
      HomePageUrl: payload.homePageUrl,
      ImageUrl: payload.imageUrl,
      Text: payload.text,
      TextFileName: payload.textFileName,
      TextFileContent: payload.textFileContent,
      ParentId: payload.parentId,
    };

    return this.http.post<Comment>(this.apiUrl, serverPayload);
  }
}
