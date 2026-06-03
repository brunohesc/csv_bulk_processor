import { ComponentFixture, TestBed } from '@angular/core/testing';
import { UploadComponent } from './upload.component';
import { MatIconModule } from '@angular/material/icon';
import { MatSnackBar } from '@angular/material/snack-bar';
import { vi } from 'vitest';

describe('UploadComponent', () => {
  let component: UploadComponent;
  let fixture: ComponentFixture<UploadComponent>;
  let snackBarSpy: any;

  beforeAll(() => {
    snackBarSpy = vi.fn();
    snackBarSpy.open = vi.fn();
  });

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UploadComponent, MatIconModule],
      providers: [
        { provide: MatSnackBar, useValue: snackBarSpy }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(UploadComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should emit fileSelected when valid file is selected via input', () => {
    const mockFile = new File(['test'], 'test.csv', { type: 'text/csv' });
    const mockEvent = { target: { files: [mockFile] } } as any;

    component.onFileInput(mockEvent);

    expect(component.selectedFile).toBe(mockFile);
  });

  it('should not emit fileSelected when non-csv file is selected', () => {
    const mockFile = new File(['test'], 'test.txt', { type: 'text/plain' });
    const mockEvent = { target: { files: [mockFile] } } as any;

    component.onFileInput(mockEvent);

    expect(component.selectedFile).toBeNull();
    expect(snackBarSpy.open).toHaveBeenCalledWith('Only CSV files are allowed', 'Close', { duration: 3000 });
  });

  it('should emit uploadCancelled when cancel is clicked', () => {
    component.selectedFile = new File(['test'], 'test.csv', { type: 'text/csv' });

    component.cancelSelection();

    expect(component.selectedFile).toBeNull();
  });

  it('should format file size correctly', () => {
    expect(component.formatFileSize(1024)).toBe('1 KB');
    expect(component.formatFileSize(1024 * 1024)).toBe('1 MB');
    expect(component.formatFileSize(1024 * 1024 * 1024)).toBe('1 GB');
  });

  it('should reset state', () => {
    component.selectedFile = new File(['test'], 'test.csv', { type: 'text/csv' });
    component.isDragging = true;

    component.reset();

    expect(component.selectedFile).toBeNull();
    expect(component.isDragging).toBeFalsy();
  });

  it('should handle drag over', () => {
    const mockEvent = { 
      preventDefault: vi.fn(),
      stopPropagation: vi.fn()
    } as any;

    component.onDragOver(mockEvent);

    expect(mockEvent.preventDefault).toHaveBeenCalled();
    expect(mockEvent.stopPropagation).toHaveBeenCalled();
    expect(component.isDragging).toBeTruthy();
  });

  it('should handle drag leave', () => {
    component.isDragging = true;
    const mockEvent = { 
      preventDefault: vi.fn(),
      stopPropagation: vi.fn()
    } as any;

    component.onDragLeave(mockEvent);

    expect(mockEvent.preventDefault).toHaveBeenCalled();
    expect(mockEvent.stopPropagation).toHaveBeenCalled();
    expect(component.isDragging).toBeFalsy();
  });

  it('should handle drop with valid file', () => {
    const mockFile = new File(['test'], 'test.csv', { type: 'text/csv' });
    const mockEvent = { 
      preventDefault: vi.fn(),
      stopPropagation: vi.fn(),
      dataTransfer: { files: [mockFile] }
    } as any;

    component.onDrop(mockEvent);

    expect(mockEvent.preventDefault).toHaveBeenCalled();
    expect(mockEvent.stopPropagation).toHaveBeenCalled();
    expect(component.selectedFile).toBe(mockFile);
    expect(component.isDragging).toBeFalsy();
  });

  it('should handle drop with invalid file', () => {
    const mockFile = new File(['test'], 'test.txt', { type: 'text/plain' });
    const mockEvent = { 
      preventDefault: vi.fn(),
      stopPropagation: vi.fn(),
      dataTransfer: { files: [mockFile] }
    } as any;

    component.onDrop(mockEvent);

    expect(mockEvent.preventDefault).toHaveBeenCalled();
    expect(mockEvent.stopPropagation).toHaveBeenCalled();
    expect(component.selectedFile).toBeNull();
    expect(snackBarSpy.open).toHaveBeenCalledWith('Only CSV files are allowed', 'Close', { duration: 3000 });
    expect(component.isDragging).toBeFalsy();
  });
});
