import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ChatMessage, ChatSendRequest, ChatSendResponse } from '../domain/assistant.model';

/**
 * Infrastructure-Service: kapselt alle HTTP-Aufrufe an `ai-service` (Gateway-Route
 * `/api/assistant/**`, siehe UserStories/ki-terminassistent.openspec.md Abschnitt 5).
 */
@Injectable({
  providedIn: 'root',
})
export class AssistantApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.assistantApiBaseUrl}/assistant`;

  sendMessage(request: ChatSendRequest): Observable<ChatSendResponse> {
    return this.http.post<ChatSendResponse>(`${this.baseUrl}/chat`, request);
  }

  getHistory(sessionId: string): Observable<ChatMessage[]> {
    return this.http.get<ChatMessage[]>(`${this.baseUrl}/sessions/${sessionId}/history`);
  }
}
