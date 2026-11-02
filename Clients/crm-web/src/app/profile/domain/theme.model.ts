/** Vordefinierte DaisyUI-Themes, umschaltbar im Profilbereich (siehe `styles.css`). */
export const BUILT_IN_THEMES = [
  'mpcrm',
  'light',
  'dark',
  'corporate',
  'emerald',
  'nord',
  'winter',
  'retro',
  'cyberpunk',
  'forest',
  'synthwave',
  'dracula',
  'night',
  'sunset',
  'coffee',
  'luxury',
] as const;
export type BuiltInTheme = (typeof BUILT_IN_THEMES)[number];

/** Eigenes, im Admin-Theme-Editor erstelltes Theme (lokal, kein Backend-Anspruch). */
export interface CustomTheme {
  id: string;
  name: string;
  primary: string;
  accent: string;
  background: string;
  text: string;
}
