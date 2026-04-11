import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { API_BASE_URL } from '../../app.config';
import { PreferencesService } from './preferences.service';
import { ThemeService } from './theme.service';
import { LanguageService } from './language.service';

describe('PreferencesService', () => {
  let service: PreferencesService;
  let httpTesting: HttpTestingController;
  let themeService: ThemeService;
  let languageService: LanguageService;

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideTranslateService({ defaultLanguage: 'es' }),
        provideTranslateHttpLoader(),
        { provide: API_BASE_URL, useValue: 'http://localhost:5001' },
        ThemeService,
        LanguageService,
        PreferencesService
      ]
    });

    service = TestBed.inject(PreferencesService);
    httpTesting = TestBed.inject(HttpTestingController);
    themeService = TestBed.inject(ThemeService);
    languageService = TestBed.inject(LanguageService);

    // Flush the initial i18n translation load triggered by LanguageService constructor
    httpTesting.match('/assets/i18n/es.json').forEach(req => req.flush({}));
  });

  afterEach(() => {
    // Flush any remaining i18n requests before verifying
    httpTesting.match((req) => req.url.includes('/assets/i18n/')).forEach(req => req.flush({}));
    httpTesting.verify();
  });

  it('should apply preferences from login response', () => {
    service.applyFromLogin({
      preferredLanguage: 'en',
      preferredTheme: 'light'
    });

    // Flush the en.json load triggered by setLanguage('en')
    httpTesting.match('/assets/i18n/en.json').forEach(req => req.flush({}));

    expect(languageService.language()).toBe('en');
    expect(themeService.theme()).toBe('light');
  });

  it('should default to es/dark for unknown values', () => {
    service.applyFromLogin({
      preferredLanguage: 'unknown',
      preferredTheme: 'unknown'
    });

    expect(languageService.language()).toBe('es');
    expect(themeService.theme()).toBe('dark');
  });

  it('should PATCH backend on updatePreference', () => {
    service.updatePreference({ theme: 'light' });

    const req = httpTesting.expectOne('http://localhost:5001/api/user/preferences');
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ theme: 'light' });
    req.flush({ language: 'es', theme: 'light' });
  });
});
