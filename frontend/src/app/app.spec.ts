import { TestBed } from '@angular/core/testing';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  it('should create the app', () => {
    const fixture = TestBed.createComponent(AppComponent);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render title', async () => {
    const fixture = TestBed.createComponent(AppComponent);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    // Check for toolbar title instead of h1
    const toolbarTitle = compiled.querySelector('.toolbar-title')?.textContent;
    expect(toolbarTitle).toBeDefined();
    if (toolbarTitle) {
      expect(toolbarTitle).toContain('Product Import System');
    }
  });
});
