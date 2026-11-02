/**
 * Domain-Entity: Benachrichtigung. Entspricht der Notifications-API, die parallel im
 * Branch `abwesenheit-workflow` (Abwesenheits-Freigabe-Workflow) entsteht.
 */
export interface Notification {
  id: string;
  message: string;
  occurredAt: string;
  read: boolean;
  link: string | null;
}
