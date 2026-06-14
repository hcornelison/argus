import { Injectable, computed, signal } from '@angular/core';
import { themeQuartz, colorSchemeDark, colorSchemeLight } from 'ag-grid-community';

/**
 * Toggles light/dark mode by adding/removing the `.dark` class on <html>, which the
 * PrimeNG Aura theme keys off (see providePrimeNG darkModeSelector in app.config).
 * The choice is persisted to localStorage and defaults to the OS preference.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  private static readonly KEY = 'argus-theme';
  readonly isDark = signal<boolean>(false);

  /** AG Grid theme — reactive, use as [theme]="theme.gridTheme()" */
  readonly gridTheme = computed(() =>
    this.isDark() ? themeQuartz.withPart(colorSchemeDark) : themeQuartz.withPart(colorSchemeLight)
  );

  /** Call once at app startup to apply the saved/preferred theme. */
  init(): void {
    const saved = localStorage.getItem(ThemeService.KEY);
    const dark = saved != null
      ? saved === 'dark'
      : window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
    this.apply(dark);
  }

  toggle(): void {
    this.apply(!this.isDark());
  }

  private apply(dark: boolean): void {
    this.isDark.set(dark);
    document.documentElement.classList.toggle('dark', dark);
    localStorage.setItem(ThemeService.KEY, dark ? 'dark' : 'light');
  }
}
