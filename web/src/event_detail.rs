use std::rc::Rc;

use i18n_embed_fl::fl;
use maplit::hashmap;
use yew::prelude::*;

use crate::{
    render::{self, MinuteTime},
    LanguageLoader,
};

#[derive(Properties, PartialEq)]
pub(super) struct EventDetailProps {
    pub start: MinuteTime,
    pub event: Rc<render::EventOccurrence>,
    pub close: Callback<()>,
}

#[function_component]
pub(super) fn EventDetail(props: &EventDetailProps) -> Html {
    let language_loader = use_context::<LanguageLoader>().unwrap();

    let close = props.close.clone();
    let close = move |_| close.emit(());
    html! {
        <div class="window">
            <div class="titlebar">
                <div class="icon">{"⏰"}</div>
                <div class="title">{&props.event.name}</div>
                <button onclick={close}>{"❌︎"}</button>
            </div>
            <div class="panel">
                if let Some(poster) = props.event.info.poster {
                    <img class="poster"
                        width={poster.width.to_string()} height={poster.height.to_string()}
                        src={format!("posters/{:02x}", poster.number)} />
                }
                <div class="details">
                    <div>{fl!(language_loader, "event_time", hashmap!{ "start" => props.start.to_string(), "duration" => props.event.duration.to_string() })}</div>
                    if let Some(description) = &props.event.info.description {
                        <pre>{description}</pre>
                    }
                    if let Some(web) = &props.event.info.web {
                        <div>{fl!(language_loader, "event_web")}<a target="_blank" href={web}>{web}</a></div>
                    }
                    if let Some(group) = &props.event.info.group {
                        <div>{fl!(language_loader, "event_group")}<a target="_blank" href={format!("https://vrc.group/{group}")}>{group}</a></div>
                    }
                    if let Some(discord) = &props.event.info.discord {
                        <div>{fl!(language_loader, "event_discord")}<a target="_blank" href={format!("https://discord.gg/{discord}")}>{discord}</a></div>
                    }
                    if let Some(hashtag) = &props.event.info.hashtag {
                        <div>{fl!(language_loader, "event_hashtag")}<a target="_blank" href={format!("https://twitter.com/hashtag/{}", hashtag.escaped())}>{"#"}{hashtag.display()}</a></div>
                    }
                    if let Some(twitter) = &props.event.info.twitter {
                        <div>{fl!(language_loader, "event_twitter")}<a target="_blank" href={format!("https://twitter.com/{twitter}")}>{twitter}</a></div>
                    }
                    if let Some(world) = &props.event.info.world {
                        <div>{fl!(language_loader, "event_world")}<a target="_blank" href={format!("https://vrchat.com/home/launch?worldId={}", world.id)}>{world.name.clone()}</a></div>
                    }
                    if !props.event.info.join.is_empty() {
                        <div>{fl!(language_loader, "event_join")}</div>
                        <ul>
                            {
                                props.event.info.join.iter().map(|j| {
                                    html!{
                                        <li><a key={&*j.id} target="_blank" href={format!("https://vrchat.com/home/user/{}", j.id)}>{j.name.clone()}</a></li>
                                    }
                                }).collect::<Html>()
                            }
                        </ul>
                    }
                </div>
            </div>
        </div>
    }
}
