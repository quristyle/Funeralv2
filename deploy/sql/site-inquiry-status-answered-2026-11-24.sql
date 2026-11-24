-- 소개 사이트 문의: 옛 상태값 answered 를 replied 로 맞춘다.
-- 서버(SiteEndpoints)와 화면(InquiryList)이 아는 값은 new · reading · replied · closed 넷뿐이라,
-- answered 로 남은 줄은 상태 거르기에 안 잡히고 배지에 영문 그대로 뜨며 일괄 처리에서도 「다른 상태」로 읽힌다.
-- DB: jsinisite (스키마 site). 멱등.
update site.inquiries
   set status = 'replied'
 where status = 'answered';
