import re

file_path = './microservices/HelpDeskServer/Endpoints/RequestEndpoints.cs'

with open(file_path, 'r', encoding='utf-8') as f:
    content = f.read()

def create_html_body(status_name, req_title, req_desc, req_id):
    return f"""var mailBody = $@"
<div style=""max-width: 600px; margin: 0 auto; font-family: 'Malgun Gothic', sans-serif; border: 1px solid #e0e0e0; border-radius: 8px; overflow: hidden;"">
    <div style=""background-color: #f8f9fa; padding: 20px; border-bottom: 1px solid #e0e0e0;"">
        <h2 style=""margin: 0; color: #333; font-size: 18px;"">[{status_name}] {{req.Title}}</h2>
    </div>
    <div style=""padding: 24px; background-color: #ffffff;"">
        <p style=""font-size: 14px; color: #555; line-height: 1.6; margin-top: 0;"">
            요청하신 내용이 <strong>{status_name}</strong> 되었습니다.
        </p>
        <div style=""background-color: #f1f3f5; padding: 16px; border-radius: 6px; margin: 20px 0;"">
            <div style=""margin: 0; font-size: 14px; color: #333; line-height: 1.5;"">
                {{req.Description}}
            </div>
        </div>
        <div style=""text-align: center; margin-top: 30px;"">
            <a href=""https://help.jin114.co.kr/helpdesk/request/detail/{{req.Id}}"" style=""display: inline-block; background-color: #007bff; color: #ffffff; text-decoration: none; padding: 12px 24px; border-radius: 4px; font-weight: bold; font-size: 14px;"">{status_name} 글 보기</a>
        </div>
    </div>
    <div style=""background-color: #f8f9fa; padding: 15px 20px; text-align: center; border-top: 1px solid #e0e0e0;"">
        <p style=""margin: 0; font-size: 12px; color: #888;"">본 메일은 발신 전용입니다. 감사합니다.</p>
    </div>
</div>";"""

# Replace InProgress
content = re.sub(
    r'string mailBody = req\.Description \+ "<br/><br/>" \+ \$\" 접수글 \[ \{req\.Title\} \] 접수되었습니다\.<br/><br/>" \+\s*\$\"<a href=\'https://help\.jin114\.co\.kr/request_detail\?id=\{req\.Id\}\' target=\'_blank\'>접수 글 보기</a><br/><br/><br/><br/>";',
    create_html_body("접수", "{req.Title}", "{req.Description}", "{req.Id}"),
    content
)

# Replace Completed
content = re.sub(
    r'string mailBody = req\.Description \+ "<br/><br/>" \+ \$\" 접수글 \[ \{req\.Title\} \] 완료되었습니다\.<br/><br/>" \+\s*\$\"<a href=\'https://help\.jin114\.co\.kr/request_detail\?id=\{req\.Id\}\' target=\'_blank\'>완료 글 보기</a><br/><br/><br/><br/>";',
    create_html_body("완료", "{req.Title}", "{req.Description}", "{req.Id}"),
    content
)

# Replace UserCompleted
content = re.sub(
    r'string mailBody = req\.Description \+ "<br/><br/>" \+ \$\" 접수글 \[ \{req\.Title\} \] 종료되었습니다\.<br/><br/>" \+\s*\$\"<a href=\'https://help\.jin114\.co\.kr/request_detail\?id=\{req\.Id\}\' target=\'_blank\'>종료 글 보기</a><br/><br/><br/><br/>";',
    create_html_body("종료", "{req.Title}", "{req.Description}", "{req.Id}"),
    content
)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)
print("done")
