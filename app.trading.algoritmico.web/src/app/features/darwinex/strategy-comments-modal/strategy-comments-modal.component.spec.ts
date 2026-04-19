import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { StrategyCommentsModalComponent } from './strategy-comments-modal.component';
import { StrategyService, StrategyCommentDto } from '../../../core/services/strategy.service';

function makeComment(id = 'c-1', content = 'Test comment'): StrategyCommentDto {
  return {
    id,
    content,
    createdAt: new Date().toISOString(),
    createdBy: 'user-1',
  };
}

describe('StrategyCommentsModalComponent', () => {
  let strategyServiceMock: Partial<StrategyService>;

  beforeEach(() => {
    strategyServiceMock = {
      getComments: vi.fn(),
      addComment: vi.fn(),
    };

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      imports: [StrategyCommentsModalComponent],
      providers: [{ provide: StrategyService, useValue: strategyServiceMock }],
    });
  });

  function create(strategyId = 'strat-1', strategyName = 'My Strategy') {
    const fixture = TestBed.createComponent(StrategyCommentsModalComponent);
    fixture.componentRef.setInput('strategyId', strategyId);
    fixture.componentRef.setInput('strategyName', strategyName);
    return fixture;
  }

  // --- Init loads comments ---

  it('ngOnInit_CallsGetCommentsWithCorrectId', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    // Act
    const fixture = create('strat-42');
    fixture.detectChanges();

    // Assert
    expect(strategyServiceMock.getComments).toHaveBeenCalledWith('strat-42');
  });

  it('ngOnInit_PopulatesCommentsSignal', () => {
    // Arrange
    const comments = [makeComment('c-1'), makeComment('c-2')];
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of(comments));

    // Act
    const fixture = create();
    fixture.detectChanges();

    // Assert
    expect(fixture.componentInstance.comments()).toEqual(comments);
    expect(fixture.componentInstance.isLoading()).toBe(false);
  });

  // --- Template rendering ---

  it('rendersCommentListWithAuthorAndContent', () => {
    // Arrange
    const comment = makeComment('c-1', 'Excellent strategy performance');
    comment.createdBy = 'trader-bob';
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([comment]));

    // Act
    const fixture = create();
    fixture.detectChanges();

    // Assert
    const authorEl = fixture.nativeElement.querySelector('.comment__author');
    const contentEl = fixture.nativeElement.querySelector('.comment__content');
    expect(authorEl?.textContent?.trim()).toBe('trader-bob');
    expect(contentEl?.textContent?.trim()).toBe('Excellent strategy performance');
  });

  it('rendersEmptyMessage_WhenNoComments', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    // Act
    const fixture = create();
    fixture.detectChanges();

    // Assert
    const emptyEl = fixture.nativeElement.querySelector('.comments__empty');
    expect(emptyEl).not.toBeNull();
  });

  it('doesNotRenderEmptyMessage_WhenCommentsExist', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(
      of([makeComment()]),
    );

    // Act
    const fixture = create();
    fixture.detectChanges();

    // Assert
    const emptyEl = fixture.nativeElement.querySelector('.comments__empty');
    expect(emptyEl).toBeNull();
  });

  // --- canSubmit ---

  it('canSubmit_EmptyContent_IsFalse', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;

    // Assert
    expect(comp.newContent()).toBe('');
    expect(comp.canSubmit()).toBe(false);
  });

  it('canSubmit_WhitespaceOnly_IsFalse', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.newContent.set('   ');

    // Assert
    expect(comp.canSubmit()).toBe(false);
  });

  it('canSubmit_WithText_IsTrue', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.newContent.set('Some insight');

    // Assert
    expect(comp.canSubmit()).toBe(true);
  });

  it('submitButton_Disabled_WhenContentEmpty', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    const fixture = create();
    fixture.detectChanges();

    // Assert
    const submitBtn = fixture.nativeElement.querySelector('.btn--primary');
    expect(submitBtn?.disabled).toBe(true);
  });

  it('submitButton_Enabled_WhenContentHasText', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.newContent.set('Some insight');
    fixture.detectChanges();

    // Assert
    const submitBtn = fixture.nativeElement.querySelector('.btn--primary');
    expect(submitBtn?.disabled).toBe(false);
  });

  // --- Submit ---

  it('onSubmit_CallsAddCommentWithTrimmedContent', () => {
    // Arrange
    const newComment = makeComment('c-new', 'Trimmed content');
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));
    (strategyServiceMock.addComment as ReturnType<typeof vi.fn>).mockReturnValue(of(newComment));

    const fixture = create('strat-1');
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.newContent.set('  Trimmed content  ');

    // Act
    comp.onSubmit();

    // Assert
    expect(strategyServiceMock.addComment).toHaveBeenCalledWith('strat-1', 'Trimmed content');
  });

  it('onSubmit_Success_PrependsToListAndClearsInput', () => {
    // Arrange
    const existing = makeComment('c-old', 'Old comment');
    const newComment = makeComment('c-new', 'New comment');

    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([existing]));
    (strategyServiceMock.addComment as ReturnType<typeof vi.fn>).mockReturnValue(of(newComment));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.newContent.set('New comment');

    // Act
    comp.onSubmit();

    // Assert
    expect(comp.comments()[0]).toEqual(newComment);
    expect(comp.comments()).toHaveLength(2);
    expect(comp.newContent()).toBe('');
    expect(comp.isSubmitting()).toBe(false);
  });

  it('onSubmit_Error_SetsErrorSignal', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));
    (strategyServiceMock.addComment as ReturnType<typeof vi.fn>).mockReturnValue(
      throwError(() => new HttpErrorResponse({ status: 400, statusText: 'Bad Request' })),
    );

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    comp.newContent.set('Some content');

    // Act
    comp.onSubmit();

    // Assert
    expect(comp.error()).not.toBeNull();
    expect(comp.isSubmitting()).toBe(false);
  });

  // --- Close ---

  it('closeButton_EmitsCloseEvent', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    let closed = false;
    comp.close.subscribe(() => {
      closed = true;
    });

    // Act
    comp.onClose();

    // Assert
    expect(closed).toBe(true);
  });

  it('closeButtonClick_EmitsCloseEvent', () => {
    // Arrange
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([]));

    const fixture = create();
    fixture.detectChanges();
    const comp = fixture.componentInstance;
    let closed = false;
    comp.close.subscribe(() => {
      closed = true;
    });

    // Act
    const closeBtn = fixture.nativeElement.querySelector('.modal__close');
    closeBtn?.click();

    // Assert
    expect(closed).toBe(true);
  });

  // --- Author fallback ---

  it('rendersUnknown_WhenCreatedByIsNull', () => {
    // Arrange
    const comment = makeComment();
    comment.createdBy = null;
    (strategyServiceMock.getComments as ReturnType<typeof vi.fn>).mockReturnValue(of([comment]));

    // Act
    const fixture = create();
    fixture.detectChanges();

    // Assert
    const authorEl = fixture.nativeElement.querySelector('.comment__author');
    expect(authorEl?.textContent?.trim()).toBe('Unknown');
  });
});
