/**
 * Die Vitest-Node-Umgebung dieses Projekts stellt kein echtes `localStorage` bereit
 * (Node 22's experimentelles `globalThis.localStorage` ist ohne `--localstorage-file`
 * nicht funktionsfaehig). Stellt einen minimalen In-Memory-Ersatz bereit, damit
 * Services, die `localStorage` nutzen (z.B. `LocalSettingsState`, `ThemeState`), in Tests
 * verifiziert werden koennen.
 */
export function installLocalStorageMock(): void {
  const store = new Map<string, string>();
  const mock: Storage = {
    get length() {
      return store.size;
    },
    clear: () => store.clear(),
    getItem: (key: string) => store.get(key) ?? null,
    key: (index: number) => Array.from(store.keys())[index] ?? null,
    removeItem: (key: string) => store.delete(key),
    setItem: (key: string, value: string) => store.set(key, value),
  };
  vi.stubGlobal('localStorage', mock);
}
