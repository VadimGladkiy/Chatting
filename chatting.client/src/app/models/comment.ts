// tree node
export interface TreeNode {
  nodeId: string;
  comment: Comment | EditedComment;
  children: TreeNode[];
  isLoading?: boolean;
}

// Interface for Comment model
export interface Comment {
  id: string;
  userName: string;
  userEmail: string;
  imageUrl: string;
  homePageUrl?: string;
  date: string;
  text: string;
  parentId: string | null;
  textFileName?: string;
  textFileContent?: string;
  errors?: FieldError[];
}

// Edited comment
export interface EditedComment extends Comment {
  isPending: true;
  clientId: string;
  textError?: string;
  textFileError?: string;
}

export interface FieldError{
  key: string,
  values: string[]
}

export interface PageInfo{
  threadEmailDescPage: number;
  threadEmailAscPage: number;
  threadUserNameDescPage: number;
  threadUserNameAscPage: number;
  threadDateDescPage: number;
  threadDateAscPage: number;
  replyDescPage: number; 
}

export interface CommentNotification {
  clientId: string,
  comment: Comment,
  pageInfo: PageInfo
}

// Interface for paginated response from the server
export interface PagedResponse {
  comments: Comment[];
  pageSize: number;
  currentPage: number;
  totalPages: number;
  totalItems: number;
}

// Interface for file attachment
export interface FileAttachment {
  textFileName?: string;
  textFileContent?: string;
}
