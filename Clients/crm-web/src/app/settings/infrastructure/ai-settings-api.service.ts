import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AiAssistantSettings, UpdateAiAssistantSettingsRequest } from '../domain/settings.model';

interface BackendAssistantSettingsDto {
  enabled: boolean;
  provider?: string;
  model: string;
  qdrantBaseUrl?: string;
  temperature?: number;
  suggestionsEnabled?: boolean;
}

/**
 * Infrastructure-Service: kapselt die Aufrufe an die KI-Einstellungen-Config-API des
 * `ai-service` (`/api/assistant/settings`). `environment.aiServiceApiBaseUrl` zeigt auf das
 * YARP-Gateway.
 */
@Injectable({ providedIn: 'root' })
export class AiSettingsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.aiServiceApiBaseUrl}/assistant/settings`;
  private lastProvider = 'OpenAi';
  private lastQdrantBaseUrl = 'http://localhost:6333';

  get(): Observable<AiAssistantSettings> {
    return this.http.get<BackendAssistantSettingsDto>(this.baseUrl).pipe(
      map((dto) => {
        if (dto.provider) this.lastProvider = dto.provider;
        if (dto.qdrantBaseUrl) this.lastQdrantBaseUrl = dto.qdrantBaseUrl;
        return {
          enabled: dto.enabled,
          model: dto.model,
          temperature: dto.temperature ?? 0.3,
          suggestionsEnabled: dto.suggestionsEnabled ?? dto.enabled,
        };
      }),
    );
  }

  update(request: UpdateAiAssistantSettingsRequest): Observable<AiAssistantSettings> {
    const payload = {
      ...request,
      provider: this.lastProvider,
      qdrantBaseUrl: this.lastQdrantBaseUrl,
    };
    return this.http.put<BackendAssistantSettingsDto>(this.baseUrl, payload).pipe(
      map((dto) => ({
        enabled: dto.enabled,
        model: dto.model,
        temperature: dto.temperature ?? request.temperature,
        suggestionsEnabled: dto.suggestionsEnabled ?? request.suggestionsEnabled,
      })),
    );
  }
}
