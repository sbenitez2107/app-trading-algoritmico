import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { MainLayoutComponent } from './main-layout.component';
import { API_BASE_URL } from '../../../app.config';
import en from '../../../../../public/assets/i18n/en.json';
import es from '../../../../../public/assets/i18n/es.json';

@Component({ template: '' })
class BlankComponent {}

const ACTIVE = 'l-sidebar__subnav-item--active';

describe('MainLayoutComponent simulator search link', () => {
  function create(lang: 'en' | 'es') {
    localStorage.setItem('preferred_language', lang);
    TestBed.configureTestingModule({
      imports: [MainLayoutComponent, TranslateModule.forRoot()],
      providers: [
        provideRouter([
          { path: 'simulator/ftmo', component: BlankComponent },
          { path: 'simulator/ftmo/search', component: BlankComponent },
        ]),
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
    fixture.componentInstance.toggleSimulator();
    fixture.detectChanges();
    return fixture;
  }

  function links(host: HTMLElement): HTMLAnchorElement[] {
    return Array.from(host.querySelectorAll<HTMLAnchorElement>('.l-sidebar__submenu a'));
  }

  async function navigate(fixture: ReturnType<typeof create>, url: string): Promise<void> {
    await TestBed.inject(Router).navigateByUrl(url);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => localStorage.removeItem('preferred_language'));

  it('theSearchEntry_FollowsTheGroupEntry_AndLinksToTheSearchRoute', () => {
    const fixture = create('en');
    const anchors = links(fixture.nativeElement);
    const group = anchors.findIndex((a) => a.getAttribute('href') === '/simulator/ftmo');
    const search = anchors.findIndex((a) => a.getAttribute('href') === '/simulator/ftmo/search');
    expect(group).toBeGreaterThanOrEqual(0);
    expect(search).toBe(group + 1);
  });

  it('theGroupLinkStillOpensTheGroupPage', () => {
    const fixture = create('en');
    const hrefs = links(fixture.nativeElement).map((a) => a.getAttribute('href'));
    expect(hrefs).toContain('/simulator/ftmo');
  });

  it('rendersTheSearchLabelFromTheRealDictionaries_EnglishAndSpanish', () => {
    for (const [lang, dictionary] of [
      ['en', en],
      ['es', es],
    ] as const) {
      TestBed.resetTestingModule();
      const fixture = create(lang);
      const anchor = links(fixture.nativeElement).find(
        (a) => a.getAttribute('href') === '/simulator/ftmo/search',
      );
      const expected = dictionary.SIMULATOR.NAV.FTMO_SEARCH;
      expect(expected).toBeTruthy();
      expect(anchor?.textContent?.trim()).toBe(expected);
      expect(anchor?.textContent).not.toContain('SIMULATOR.');
    }
  });

  it('onTheSearchPage_OnlyTheSearchLinkIsActive', async () => {
    const fixture = create('en');
    await navigate(fixture, '/simulator/ftmo/search');
    const anchors = links(fixture.nativeElement);
    const group = anchors.find((a) => a.getAttribute('href') === '/simulator/ftmo');
    const search = anchors.find((a) => a.getAttribute('href') === '/simulator/ftmo/search');
    expect(search?.classList.contains(ACTIVE)).toBe(true);
    expect(group?.classList.contains(ACTIVE)).toBe(false);
  });

  it('onTheGroupPage_OnlyTheGroupLinkIsActive', async () => {
    const fixture = create('en');
    await navigate(fixture, '/simulator/ftmo');
    const anchors = links(fixture.nativeElement);
    const group = anchors.find((a) => a.getAttribute('href') === '/simulator/ftmo');
    const search = anchors.find((a) => a.getAttribute('href') === '/simulator/ftmo/search');
    expect(group?.classList.contains(ACTIVE)).toBe(true);
    expect(search?.classList.contains(ACTIVE)).toBe(false);
  });
});
