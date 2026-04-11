import { ThemeService } from './theme.service';

describe('ThemeService', () => {
  let service: ThemeService;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
  });

  it('should default to dark when localStorage is empty', () => {
    service = new ThemeService();
    expect(service.theme()).toBe('dark');
    expect(service.isDark()).toBe(true);
  });

  it('should read theme from localStorage', () => {
    localStorage.setItem('preferred_theme', 'light');
    service = new ThemeService();
    expect(service.theme()).toBe('light');
    expect(service.isDark()).toBe(false);
  });

  it('should set data-theme attribute on document', () => {
    service = new ThemeService();
    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('should toggle between dark and light', () => {
    service = new ThemeService();
    expect(service.isDark()).toBe(true);

    service.toggleTheme();
    expect(service.theme()).toBe('light');
    expect(document.documentElement.getAttribute('data-theme')).toBe('light');

    service.toggleTheme();
    expect(service.theme()).toBe('dark');
  });

  it('should persist to localStorage on setTheme', () => {
    service = new ThemeService();
    service.setTheme('light');
    expect(localStorage.getItem('preferred_theme')).toBe('light');
  });

  it('should fallback to dark for invalid localStorage value', () => {
    localStorage.setItem('preferred_theme', 'invalid');
    service = new ThemeService();
    expect(service.theme()).toBe('dark');
  });
});
