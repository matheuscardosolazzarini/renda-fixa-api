# Validações das variáveis do esqueleto (D4 e "Estrutura de infra/aws/").
# O mock_provider evita qualquer chamada real ao provider. O mock_data fixa as AZs, que
# o módulo network exige (a lista viria vazia com o mock).

mock_provider "aws" {
  mock_data "aws_availability_zones" {
    defaults = {
      names = ["us-east-1a", "us-east-1b", "us-east-1c"]
    }
  }

  # O módulo app (P3) monta o ARN da execution role com o id da conta.
  mock_data "aws_caller_identity" {
    defaults = {
      account_id = "123456789012"
    }
  }
}

variables {
  image_sha = "0123456789abcdef0123456789abcdef01234567"
}

run "expires_at_dentro_do_prazo_e_aceito" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
  }

  assert {
    condition     = output.expires_at == var.expires_at
    error_message = "o output expires_at deve repetir a variável"
  }
}

run "expires_at_no_limite_inferior_do_prazo_e_aceito" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "11h")
  }
}

# Fronteira exata de 12 h: o teste usa o mesmo relógio da validação (a diferença entre timestamp() e
# plantimestamp() é de milissegundos, bem abaixo de 1 minuto).
run "expires_at_em_exatamente_12h_e_aceito" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "12h")
  }
}

run "expires_at_12h_mais_1_minuto_e_rejeitado" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "12h1m")
  }

  expect_failures = [var.expires_at]
}

run "expires_at_no_passado_e_aceito_como_marcacao_de_destruicao" {
  command = plan

  variables {
    expires_at = "1970-01-01T00:00:00Z"
  }
}

run "expires_at_acima_de_12h_e_rejeitado" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "13h")
  }

  expect_failures = [var.expires_at]
}

run "expires_at_fora_do_formato_rfc3339_e_rejeitado" {
  command = plan

  variables {
    expires_at = "amanha as 10h"
  }

  expect_failures = [var.expires_at]
}

run "service_desired_count_padrao_e_zero" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
  }

  assert {
    condition     = var.service_desired_count == 0
    error_message = "o padrão de service_desired_count deve ser 0 (D3)"
  }
}

run "service_desired_count_um_e_aceito" {
  command = plan

  variables {
    expires_at            = timeadd(timestamp(), "4h")
    service_desired_count = 1
  }
}

run "service_desired_count_dois_e_rejeitado" {
  command = plan

  variables {
    expires_at            = timeadd(timestamp(), "4h")
    service_desired_count = 2
  }

  expect_failures = [var.service_desired_count]
}

run "service_desired_count_negativo_e_rejeitado" {
  command = plan

  variables {
    expires_at            = timeadd(timestamp(), "4h")
    service_desired_count = -1
  }

  expect_failures = [var.service_desired_count]
}

run "image_sha_com_40_hexadecimais_e_aceito" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
    image_sha  = "abcdef0123456789abcdef0123456789abcdef01"
  }

  assert {
    condition     = output.image_sha == var.image_sha
    error_message = "o output image_sha deve repetir a variável"
  }
}

run "image_sha_curto_e_rejeitado" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
    image_sha  = "abc123"
  }

  expect_failures = [var.image_sha]
}

run "image_sha_com_caractere_nao_hexadecimal_e_rejeitado" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
    image_sha  = "g123456789abcdef0123456789abcdef01234567"
  }

  expect_failures = [var.image_sha]
}

run "image_sha_com_tag_mutavel_e_rejeitado" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
    image_sha  = "latest"
  }

  expect_failures = [var.image_sha]
}

run "image_sha_com_sufixo_e_rejeitado" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
    image_sha  = "0123456789abcdef0123456789abcdef01234567-migrator"
  }

  expect_failures = [var.image_sha]
}

run "region_padrao_e_us_east_1" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
  }

  assert {
    condition     = var.region == "us-east-1"
    error_message = "a região padrão deve ser us-east-1 (D5)"
  }
}

run "region_invalida_e_rejeitada" {
  command = plan

  variables {
    expires_at = timeadd(timestamp(), "4h")
    region     = "Virginia"
  }

  expect_failures = [var.region]
}
