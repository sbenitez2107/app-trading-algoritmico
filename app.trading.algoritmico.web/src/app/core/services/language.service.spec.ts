import { TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';
import { provideTranslateHttpLoader } from '@ngx-translate/http-loader';
import { provideHttpClient } from '@angular/common/http';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  let service: LanguageService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideTranslateService({ defaultLanguage: 'es' }),
        provideTranslateHttpLoader(),
        LanguageService
      ]
    });
  });

  it('should default to es when localStorage is empty', () => {
    service = TestBed.inject(LanguageService);
    expect(service.language()).toBe('es');
    expect(service.isSpanish()).toBe(true);
  });

  it('should read language from localStorage', () => {
    localStorage.setItem('preferred_language', 'en');
    service = TestBed.inject(LanguageService);
    expect(service.language()).toBe('en');
    expect(service.isSpanish()).toBe(false);
  });

  it('should toggle between es and en', () => {
    service = TestBed.inject(LanguageService);
    expect(service.language()).toBe('es');

    service.toggleLanguage();
    expect(service.language()).toBe('en');

    service.toggleLanguage();
    expect(service.language()).toBe('es');
  });

  it('should persist to localStorage on setLanguage', () => {
    service = TestBed.inject(LanguageService);
    service.setLanguage('en');
    expect(localStorage.getItem('preferred_language')).toBe('en');
  });

  it('should fallback to es for invalid localStorage value', () => {
    localStorage.setItem('preferred_language', 'fr');
    service = TestBed.inject(LanguageService);
    expect(service.language()).toBe('es');
  });
});
