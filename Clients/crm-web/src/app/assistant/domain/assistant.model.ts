/**
 * Domain-Modelle des KI-Terminassistenten (UserStories/ki-terminassistent.openspec.md, Abschnitt 5).
 * Spiegeln die DTOs von `ai-service` 1:1 (`ChatMessageDto`/`AppointmentSuggestionDto`).
 */
export type ChatMessageRole = 'user' | 'assistant';

export interface ChatMessage {
  sessionId: string;
  role: ChatMessageRole;
  content: string;
  createdAt: string;
}

export interface AppointmentSuggestion {
  employeeId: string;
  employeeName: string;
  serviceId: string;
  serviceName: string;
  customerId: string | null;
  startTime: string;
  endTime: string;
}

export interface ChatSendRequest {
  sessionId: string | null;
  message: string;
}

export interface ChatSendResponse {
  reply: string;
  suggestions: AppointmentSuggestion[];
}

/** UI-Darstellung einer gesendeten/empfangenen Nachricht im Chat-Verlauf. */
export interface DisplayChatMessage {
  role: ChatMessageRole;
  content: string;
  suggestions: AppointmentSuggestion[];
}
