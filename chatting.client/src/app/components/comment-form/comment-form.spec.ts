import { ComponentFixture, TestBed } from '@angular/core/testing';
import { EMPTY, Subject } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { CommentFormComponent } from './comment-form';
import { EditedComment } from '../../models/comment';
import { CommentApiService } from '../../services/comment-api-service';
import { CommentWsService } from '../../services/comment-ws-service';
import { ModalTriggerService } from '../../services/modal-trigger-service';

describe('CommentFormComponent tag validation', () => {
  let fixture: ComponentFixture<CommentFormComponent>;
  let component: CommentFormComponent;

  const validInputs: Array<[string, string]> = [
    ['plain text', 'A plain comment'],
    ['one strong tag', '<strong>bold</strong>'],
    ['one italic tag', '<i>italic</i>'],
    ['one code tag', '<code>code</code>'],
    ['an anchor tag', '<a href="https://example.com">link</a>'],
    ['nested tags', '<i>italic <strong>bold</strong></i>'],
    ['multiple sibling tags', '<i>one</i> and <code>two</code>'],
    ['case-insensitive matching tags', '<I>italic</i>']
  ];

  const invalidInputs: Array<[string, string, string]> = [
    ['an unclosed strong tag', '<strong>bold', 'Missing closing tag for <strong>.'],
    ['an unclosed italic tag', '<i> fhrthnbfdb fbrbgrfb', 'Missing closing tag for <i>.'],
    ['an unclosed code tag', '<code>code', 'Missing closing tag for <code>.'],
    ['an unclosed anchor tag', '<a href="https://example.com">link', 'Missing closing tag for <a>.'],
    ['an unmatched strong closing tag', '</strong>text', 'Closing tag </strong> does not match its opening tag.'],
    ['an unmatched italic closing tag', '</i>text', 'Closing tag </i> does not match its opening tag.'],
    ['an unmatched code closing tag', '</code>text', 'Closing tag </code> does not match its opening tag.'],
    ['an unmatched anchor closing tag', '</a>text', 'Closing tag </a> does not match its opening tag.'],
    ['a mismatched closing tag', '<i>text</strong>', 'Closing tag </strong> does not match its opening tag.'],
    ['incorrectly nested tags', '<i><strong>text</i></strong>', 'Closing tag </i> does not match its opening tag.'],
    ['an extra closing tag', '<i>text</i></i>', 'Closing tag </i> does not match its opening tag.'],
    ['an unsupported opening tag', '<b>bold</b>', 'Invalid tag: <b>. Allowed tags: <strong>, <i>, <code>, <a href="...">'],
    ['an unsupported closing tag', '<i>text</b>', 'Invalid tag: <b>. Allowed tags: <strong>, <i>, <code>, <a href="...">'],
    ['an empty anchor href', '<a href=""></a>', 'The <a> tag must have a valid absolute http or https href.'],
    ['a whitespace-only anchor href', '<a href="   ">link</a>', 'The <a> tag must have a valid absolute http or https href.'],
    ['a missing anchor href', '<a>link</a>', 'The <a> tag must have a valid absolute http or https href.'],
    ['a relative anchor href', '<a href="/page">link</a>', 'The <a> tag must have a valid absolute http or https href.'],
    ['an invalid anchor href', '<a href="not a url">link</a>', 'The <a> tag must have a valid absolute http or https href.'],
    ['an empty strong tag', '<strong></strong>', 'The <strong> tag must contain text.'],
    ['an empty italic tag', '<i></i>', 'The <i> tag must contain text.'],
    ['a whitespace-only tag', '<code>   </code>', 'The <code> tag must contain text.'],
    ['an empty nested tag', '<strong><i></i></strong>', 'The <i> tag must contain text.']
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CommentFormComponent],
      providers: [
        { provide: ModalTriggerService, useValue: { notifyModel$: EMPTY } },
        { provide: CommentApiService, useValue: { clientId: 'test-client', getCaptcha: () => EMPTY } },
        { provide: CommentWsService, useValue: { validateCaptcha: vi.fn() } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(CommentFormComponent);
    component = fixture.componentInstance;
  });

  function validateTagsDirectly(text: string): string | null {
    const testableComponent = component as unknown as {
      validateTags(value: string): string | null;
    };

    return testableComponent.validateTags.call(component, text);
  }

  async function validateThroughSubmit(text: string): Promise<string | null> {
    component.model = {
      clientId: 'test-client',
      id: 'test-comment',
      userName: 'Test User',
      userEmail: 'test@example.com',
      imageUrl: '',
      date: '',
      text,
      parentId: null,
      isPending: true,
      textError: ''
    } satisfies EditedComment;
    component.responseChannel = new Subject<EditedComment | null>();

    const event = {
      preventDefault: vi.fn(),
      stopPropagation: vi.fn()
    } as unknown as Event;

    await component.onSubmit(event, { invalid: false });
    return component.model.textError || null;
  }

  it('returns no tag error for empty text', () => {
    expect(validateTagsDirectly('')).toBeNull();
  });

  it.each(validInputs)('accepts %s', async (_description, text) => {
    expect(await validateThroughSubmit(text)).toBeNull();
  });

  it.each(invalidInputs)('rejects %s', async (_description, text, expectedError) => {
    expect(await validateThroughSubmit(text)).toBe(expectedError);
  });
});
