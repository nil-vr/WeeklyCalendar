use std::rc::Rc;

use i18n_embed_fl::fl;
use yew::prelude::*;

use crate::{LanguageLoader, MetadataRoot};

#[derive(Properties, PartialEq)]
pub(super) struct AboutProps {
    pub metadata: Rc<MetadataRoot>,
    pub language: Option<AttrValue>,
    pub close: Callback<()>,
}

#[function_component]
pub(super) fn About(props: &AboutProps) -> Html {
    let language_loader = use_context::<LanguageLoader>().unwrap();

    let close = props.close.clone();
    let close = move |_| close.emit(());

    let language_meta = props
        .language
        .as_deref()
        .and_then(|l| props.metadata.lang.get(l));
    let name = language_meta
        .and_then(|m| m.title.clone())
        .or_else(|| props.metadata.meta.title.clone());
    let description = language_meta
        .and_then(|m| m.desc.clone())
        .or_else(|| props.metadata.meta.desc.clone());
    let link = language_meta
        .and_then(|m| m.link.clone())
        .or_else(|| props.metadata.meta.link.clone());

    html! {
        <div class="window">
            <div class="titlebar">
                <div class="icon">{"ⓘ"}</div>
                <div class="title">{"About"}</div>
                <button onclick={close}>{"❌︎"}</button>
            </div>
            <div class="about">
                <h1>{name}</h1>
                <pre>{description}</pre>
                <a target="_blank" href={link}>{fl!(language_loader, "about_more")}</a>
                <h1>{"Credits"}</h1>
                <ul>
                <li><a target="_blank" href="https://github.com/nil-vr/WeeklyCalendar/">{"Weekly Calendar"}</a></li>
                <li><a target="_blank" href="https://fonts.google.com/noto">{"Noto Sans"}</a></li>
                </ul>
            </div>
        </div>
    }
}
