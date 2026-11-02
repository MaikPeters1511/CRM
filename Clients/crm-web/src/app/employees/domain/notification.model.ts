/**
 * Domain-Modell einer Benachrichtigung. Entspricht `NotificationDto`
 * (`employee-service`, abwesenheit-workflow.openspec.md Abschnitt 5). Eigenstaendiges
 * Aggregate (kein Teil des `Employee`-Aggregats) - siehe Requirement "Benachrichtigung
 * bei Entscheidung ueber Abwesenheitsanfrage".
 */
export interface Notification {
  id: string;
  recipientEmployeeId: string;
  text: string;
  link: string | null;
  isRead: boolean;
  createdAt: string;
}
