import { TestBed } from '@angular/core/testing';
import { HttpClientTestingModule, HttpTestingController } from '@angular/common/http/testing';
import { ApiService } from './api.service';
import { Product, ProductFilter } from '../models/product.model';

describe('ApiService', () => {
  let service: ApiService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [HttpClientTestingModule],
      providers: [ApiService]
    });
    service = TestBed.inject(ApiService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should upload file and return job id', () => {
    const mockFile = new File(['test'], 'test.csv', { type: 'text/csv' });
    const mockResponse = { importJobId: '123e4567-e89b-12d3-a456-426614174000' };

    service.uploadFile(mockFile).subscribe(response => {
      expect(response.importJobId).toBe(mockResponse.importJobId);
    });

    const req = httpMock.expectOne(service['baseUrl'] + '/import/upload');
    expect(req.request.method).toBe('POST');
    req.flush(mockResponse);
  });

  it('should cancel import', () => {
    const jobId = '123e4567-e89b-12d3-a456-426614174000';

    service.cancelImport(jobId).subscribe();

    const req = httpMock.expectOne(service['baseUrl'] + `/import/${jobId}/cancel`);
    expect(req.request.method).toBe('POST');
    req.flush({});
  });

  it('should get products with filters', () => {
    const filter: ProductFilter = {
      nameFilter: 'Product A',
      minPrice: 10,
      maxPrice: 100,
      page: 1,
      pageSize: 50
    };
    const mockResponse = {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 50,
      totalPages: 0
    };

    service.getProducts(filter).subscribe(response => {
      expect(response).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(req => 
      req.url.includes('/products') && 
      req.method === 'GET'
    );
    expect(req.request.params.get('nameFilter')).toBe('Product A');
    expect(req.request.params.get('minPrice')).toBe('10');
    expect(req.request.params.get('maxPrice')).toBe('100');
    req.flush(mockResponse);
  });

  it('should get product by id', () => {
    const productId = '123e4567-e89b-12d3-a456-426614174000';
    const mockResponse = {
      id: productId,
      name: 'Product A',
      originalPrice: 100,
      expirationDate: '2024-12-31',
      convertedPrices: { USD: 100 },
      createdAt: '2024-01-01T00:00:00Z',
      importJobId: 'job-id'
    };

    service.getProduct(productId).subscribe(response => {
      expect(response).toEqual(mockResponse);
    });

    const req = httpMock.expectOne(service['baseUrl'] + `/products/${productId}`);
    expect(req.request.method).toBe('GET');
    req.flush(mockResponse);
  });
});
