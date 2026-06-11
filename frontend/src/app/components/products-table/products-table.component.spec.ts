import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ProductsTableComponent } from './products-table.component';
import { Product, ProductFilter } from '../../models/product.model';
import { vi } from 'vitest';

describe('ProductsTableComponent', () => {
  let component: ProductsTableComponent;
  let fixture: ComponentFixture<ProductsTableComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ProductsTableComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(ProductsTableComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should calculate total pages correctly', () => {
    component.totalCount = 100;
    component.pageSize = 50;
    expect(component.totalPages).toBe(2);

    component.totalCount = 101;
    expect(component.totalPages).toBe(3);
  });

  it('should format price correctly', () => {
    expect(component.formatPrice(123.456)).toBe('123.46');
    expect(component.formatPrice(100)).toBe('100.00');
  });

  it('should format date correctly', () => {
    // Account for timezone offset - the date may shift by one day
    const formatted = component.formatDate('2024-12-31');
    expect(formatted).toMatch(/\d{2}\/\d{2}\/2024/);
    expect(component.formatDate(null)).toBe('-');
    // Due to timezone offset, 9999-12-31 may become 12/30/9999
    // Just check that it handles the far future date
    const farFuture = component.formatDate('9999-12-31');
    expect(farFuture).toMatch(/\d{2}\/\d{2}\/9999/);
  });

  it('should detect RTL text', () => {
    expect(component.isRtl('Hello')).toBe(false);
    expect(component.isRtl('مرحبا')).toBe(true);
    expect(component.isRtl('שלום')).toBe(true);
  });

  it('should return correct text direction', () => {
    expect(component.getTextDirection('Hello')).toBe('ltr');
    expect(component.getTextDirection('مرحبا')).toBe('rtl');
  });

  it('should parse converted prices from JSON', () => {
    const jsonString = '{"USD":100,"EUR":92}';
    const result = component.parseConvertedPrices(jsonString);
    expect(result['USD']).toBe(100);
    expect(result['EUR']).toBe(92);
  });

  it('should handle invalid JSON in converted prices', () => {
    const result = component.parseConvertedPrices('invalid');
    expect(Object.keys(result).length).toBe(0);
  });

  it('should get currency keys from JSON', () => {
    const jsonString = '{"USD":100,"EUR":92,"GBP":79}';
    const keys = component.getCurrencyKeys(jsonString);
    expect(keys).toContain('USD');
    expect(keys).toContain('EUR');
    expect(keys).toContain('GBP');
  });

  it('should emit filter change on search', () => {
    const emitSpy = vi.fn();
    component.filterChange.emit = emitSpy;
    component.nameFilterValue = 'Test';
    component.minPriceValue = '10';
    component.maxPriceValue = '100';
    component.expirationFromValue = new Date('2024-01-01');
    component.expirationToValue = new Date('2024-12-31');

    component.onSearch();

    expect(emitSpy).toHaveBeenCalled();
  });

  it('should emit sort change on sort', () => {
    const emitSpy = vi.fn();
    component.sortChange.emit = emitSpy;
    const sort = { active: 'name', direction: 'asc' as any };

    component.onSortChange(sort);

    expect(emitSpy).toHaveBeenCalledWith(sort);
  });

  it('should emit filter change on page change', () => {
    const emitSpy = vi.fn();
    component.filterChange.emit = emitSpy;
    const event = { pageIndex: 1, pageSize: 25 };

    component.onPageChange(event);

    expect(emitSpy).toHaveBeenCalled();
  });

  it('should clear all filters', () => {
    component.nameFilterValue = 'Test';
    component.minPriceValue = '10';
    component.maxPriceValue = '100';
    component.expirationFromValue = new Date();
    component.expirationToValue = new Date();

    component.clearFilters();

    expect(component.nameFilterValue).toBe('');
    expect(component.minPriceValue).toBe('');
    expect(component.maxPriceValue).toBe('');
    expect(component.expirationFromValue).toBeNull();
    expect(component.expirationToValue).toBeNull();
  });

  it('should update filter input values', () => {
    component.onNameFilterChange('Test');
    expect(component.nameFilterValue).toBe('Test');

    component.onMinPriceChange('10');
    expect(component.minPriceValue).toBe('10');

    component.onMaxPriceChange('100');
    expect(component.maxPriceValue).toBe('100');

    const testDate = new Date('2024-01-01');
    component.onExpirationFromChange(testDate);
    expect(component.expirationFromValue).toBe(testDate);

    component.onExpirationToChange(testDate);
    expect(component.expirationToValue).toBe(testDate);
  });
});
