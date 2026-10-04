import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MainLayoutComponent } from './main-layout.component';
import { API_BASE_URL } from '../../../app.config';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

/** Only the Simulator sidebar group is covered here; there was no layout spec before F1. */
describe('MainLayoutComponent simulator group', () => {
  function create(lang: 'en' | 'es') {
    localStorage.setItem('preferred_language', lang);
    TestBed.configureTestingModule({
      imports: [MainLayoutComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://localhost/api-test' },
      ],
    });
    const translate = TestBed.inject(TranslateService);
    translate.setTranslation('en', en);
    translate.setTranslation('es', es);
    translate.use(lang);
    const fixture = TestBed.createComponent(MainLayoutComponent);
    fixture.detectChanges();
    return fixture;
  }

  function groupButtons(host: HTMLElement): HTMLElement[] {
    return Array.from(host.querySelectorAll('.l-sidebar__nav-group > button'));
  }

  afterEach(() => localStorage.removeItem('preferred_language'));

  it('theSimulatorGroup_SitsRightAfterTheFtmoGroup', () => {
    const fixture = create('en');
    const labels = groupButtons(fixture.nativeElement).map((b) => b.textContent?.trim() ?? '');
    const ftmo = labels.findIndex((t) => t.includes('FTMO'));
    const simulator = labels.findIndex((t) => t.includes('Simulator'));
    expect(ftmo).toBeGreaterThanOrEqual(0);
    expect(simulator).toBe(ftmo + 1);
  });

  it('simulatorExpanded_StartsCollapsedAndToggles', () => {
    const fixture = create('en');
    const cmp = fixture.componentInstance;
    expect(cmp.simulatorExpanded()).toBe(false);
    cmp.toggleSimulator();
    expect(cmp.simulatorExpanded()).toBe(true);
    cmp.toggleSimulator();
    expect(cmp.simulatorExpanded()).toBe(false);
  });

  it('expanded_ShowsALinkToSimulatorFtmo_WithTranslatedText', () => {
    const fixture = create('en');
    fixture.componentInstance.toggleSimulator();
    fixture.detectChanges();
    const link = (fixture.nativeElement as HTMLElement).querySelector(
      'a[href="/simulator/ftmo"]',
    ) as HTMLElement;
    expect(link).not.toBeNull();
    expect(link.textContent?.trim()).toBe('FTMO group');
  });

  it('toggleSidebar_CollapsesTheSimulatorGroup', () => {
    const fixture = create('en');
    const cmp = fixture.componentInstance;
    cmp.toggleSimulator();
    expect(cmp.simulatorExpanded()).toBe(true);
    cmp.toggleSidebar();
    expect(cmp.sidebarCollapsed()).toBe(true);
    expect(cmp.simulatorExpanded()).toBe(false);
    cmp.toggleSimulator();
    expect(cmp.simulatorExpanded()).toBe(false);
  });

  it('theLabelsRenderSpanishText_WithNoRawKeyAndNoPlaceholder', () => {
    const fixture = create('es');
    fixture.componentInstance.toggleSimulator();
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Simulador');
    expect(text).toContain('Grupo FTMO');
    expect(text).not.toContain('SIMULATOR.');
    expect(text).not.toContain('{{');
  });
});
