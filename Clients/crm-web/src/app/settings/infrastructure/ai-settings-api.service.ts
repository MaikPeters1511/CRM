import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AiAssistantSettings, UpdateAiAssistantSettingsRequest } from '../domain/settings.model';

/**
 * Infrastructure-Service: kapselt die Aufrufe an die KI-Einstellungen-Config-API des
 * `ai-service` (entsteht parallel im Branch `ki-einstellungen-backend`, siehe Background
 * dieser Session). `environment.aiServiceApiBaseUrl` zeigt bereits auf das YARP-Gateway -
 * solange der Endpunkt dort noch nicht existiert, liefert der Aufruf 404/503, was die
 * Settings-Komponente als Error-State behandelt statt abzustuerzen.
 */
@Injectable({ providedIn: 'root' })
export class AiSettingsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.aiServiceApiBaseUrl}/ai-assistant/settings`;

  get(): Observable<AiAssistantSettings> {
    return this.http.get<AiAssistantSettings>(this.baseUrl);
  }

  update(request: UpdateAiAssistantSettingsRequest): Observable<AiAssistantSettings> {
    return this.http.put<AiAssistantSettings>(this.baseUrl, request);
  }
}
